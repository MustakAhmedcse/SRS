namespace SalesCom.Infrastructure.Data;

using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Maps a <see cref="JsonDocument"/> domain property to a Postgres <c>jsonb</c> column. Stored as
/// the raw JSON text; rehydrated with <see cref="JsonDocument.Parse(string,JsonDocumentOptions)"/>.
/// Shared by every aggregate that keeps dynamic logic in JSON.
/// </summary>
internal sealed class JsonDocumentConverter() : ValueConverter<JsonDocument, string>(
    document => document.RootElement.GetRawText(),
    text => JsonDocument.Parse(text, default));
