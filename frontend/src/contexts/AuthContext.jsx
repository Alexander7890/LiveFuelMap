import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { api, clearAuthStorage, getRefreshToken, persistAuth } from "../services/api";

const AuthContext = createContext(null);

function userFromAuthResult(result) {
  const userId = Number(result?.userId);
  if (!Number.isFinite(userId) || !result?.email || !result?.role) return null;

  const requiresNicknameSetup = Boolean(result.requiresNickname || result.requiresNicknameSetup);
  const nickname = requiresNicknameSetup ? "" : (result.nickname || result.suggestedNickname || result.email.split("@")[0]);
  return {
    userId,
    email: result.email,
    role: result.role,
    emailConfirmed: true,
    displayName: nickname,
    nickname,
    profileImageUrl: "",
    canSubscribeToEmail: true,
    authProvider: result.authProvider || "Local",
    requiresNicknameSetup
  };
}

export function AuthProvider({ children }) {
  const [currentUser, setCurrentUser] = useState(null);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);

  const loadCurrentUser = useCallback(async () => {
    try {
      const user = await api.auth.verify();
      setCurrentUser(user);
      return user;
    } catch {
      clearAuthStorage();
      setCurrentUser(null);
      setProfile(null);
      return null;
    }
  }, []);

  const loadProfile = useCallback(async () => {
    try {
      const data = await api.profile.get();
      setProfile(data);
      return data;
    } catch {
      setProfile(null);
      return null;
    }
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      if (!getRefreshToken()) {
        if (active) setLoading(false);
        return;
      }

      const user = await loadCurrentUser();
      if (user) await loadProfile();
      if (active) setLoading(false);
    })();

    return () => {
      active = false;
    };
  }, [loadCurrentUser, loadProfile]);

  const completeAuth = useCallback(async (result, options = {}) => {
    persistAuth(result);
    const provisionalUser = userFromAuthResult(result);
    if (provisionalUser) {
      setCurrentUser(provisionalUser);
      setLoading(false);
    }

    if (options.verify === false) {
      return provisionalUser;
    }

    const user = await loadCurrentUser();
    await loadProfile();
    setLoading(false);
    return user ?? provisionalUser;
  }, [loadCurrentUser, loadProfile]);

  const login = useCallback(async request => {
    const result = await api.auth.login(request);
    return completeAuth(result);
  }, [completeAuth]);

  const register = useCallback(async request => {
    return api.auth.register(request);
  }, []);

  const logout = useCallback(async () => {
    const refreshToken = getRefreshToken();
    try {
      if (refreshToken) await api.auth.logout(refreshToken);
    } catch {
      // Local logout should still happen if the token is already invalid.
    }
    clearAuthStorage();
    setCurrentUser(null);
    setProfile(null);
  }, []);

  const refreshProfile = useCallback(async () => {
    await loadCurrentUser();
    return loadProfile();
  }, [loadCurrentUser, loadProfile]);

  const value = useMemo(() => ({
    currentUser,
    profile,
    loading,
    isLoggedIn: Boolean(currentUser),
    isAdmin: currentUser?.role === "Admin",
    login,
    register,
    completeAuth,
    logout,
    refreshProfile,
    setProfile
  }), [currentUser, profile, loading, login, register, completeAuth, logout, refreshProfile]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside AuthProvider");
  return context;
}
