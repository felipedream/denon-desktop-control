// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Collections.Generic;

namespace DenonRemote.Heos;

/// <summary>A HEOS music source: streaming service, local server or input.</summary>
public sealed record MusicSource(
    string Name,
    string Sid,
    string ImageUrl,
    string Type,
    bool Available,
    string Username);

/// <summary>
/// One row in a browse listing. Can be a container the user drills into
/// (album, playlist, genre) or a playable item (track, station).
/// </summary>
public sealed record BrowseItem(
    string Name,
    string Sid,
    string Cid,
    string Mid,
    string ImageUrl,
    string Type,
    bool IsContainer,
    bool IsPlayable)
{
    /// <summary>Secondary line shown under the name in the list.</summary>
    public string Subtitle => Type switch
    {
        "song"      => "Cancion",
        "album"     => "Album",
        "artist"    => "Artista",
        "playlist"  => "Lista",
        "station"   => "Emisora",
        "container" => "Carpeta",
        "heos_service" => "Servicio",
        _ => Type
    };
}

/// <summary>An entry in the playback queue.</summary>
public sealed record QueueItem(
    string QueueId,
    string Title,
    string Artist,
    string Album,
    string ImageUrl);

/// <summary>A way to search a service, e.g. "Artist" with scid 1.</summary>
public sealed record SearchCriteria(string Name, string Scid);

/// <summary>One level in the browse trail, used for the back navigation.</summary>
public sealed record BrowseCrumb(string Label, string Sid, string? Cid);
