using System.Text.Json.Serialization;

namespace GpsImageExtractor.Models;

public sealed record GpsMarker(
    [property: JsonPropertyName("marker_uuid")] string Uuid,
    [property: JsonPropertyName("marker_latitude")] double Latitude,
    [property: JsonPropertyName("marker_longitude")] double Longitude,
    [property: JsonPropertyName("marker_is_favorite")] bool IsFavorite,
    [property: JsonPropertyName("marker_favorite_title")] string Title,
    [property: JsonPropertyName("marker_favorite_description")] string Description);