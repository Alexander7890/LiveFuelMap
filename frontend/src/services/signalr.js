import * as signalR from "@microsoft/signalr";
import { SIGNALR_HUB_URL } from "../config";
import { getAccessToken } from "./api";

export function createFuelHubConnection() {
  return new signalR.HubConnectionBuilder()
    .withUrl(SIGNALR_HUB_URL, {
      accessTokenFactory: () => getAccessToken() || ""
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}

