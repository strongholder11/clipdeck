using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipDeck.Core.Models;

/// <summary>
/// Datas no formato que a versão macOS entende: UTC, sem fração de segundo
/// ("2026-08-19T12:07:49Z").
///
/// O padrão do .NET grava "2026-08-19T09:07:49.1234567-03:00", e o decodificador
/// ISO 8601 do Swift recusa a fração de segundo — um arquivo exportado no Windows
/// seria rejeitado inteiro ao importar no Mac. A leitura continua aceitando
/// qualquer variante ISO 8601.
/// </summary>
public sealed class Iso8601DateConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
}
