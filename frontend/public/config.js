window.LiveFuelMapConfig = {
  apiBaseUrl: "http://localhost:5000",
  signalRHubUrl: "http://localhost:5000/hubs/fuel",
  googleMapsApiKey: "",
  googlePlacesSearchAreas: [
    { name: "Харків", lat: 49.9935, lng: 36.2304, radius: 50000 },
    { name: "Ізюм", lat: 49.2088, lng: 37.2485, radius: 35000 },
    { name: "Лозова", lat: 48.8894, lng: 36.3176, radius: 35000 },
    { name: "Красноград", lat: 49.3801, lng: 35.4567, radius: 35000 },
    { name: "Куп'янськ", lat: 49.7106, lng: 37.6156, radius: 35000 }
  ]
};
