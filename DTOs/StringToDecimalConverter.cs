using System.Text.Json;
using System.Text.Json.Serialization;

namespace DTOs;

/// <summary>
/// Conversor que lida com valores que podem vir como string (ex: "5.00") ou número (5.00)
/// e converte para decimal.
/// </summary>
public class StringToDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Se vier como string
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                return d;

            throw new JsonException($"Não foi possível converter '{s}' para decimal.");
        }

        // Se vier como número
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        throw new JsonException($"Token inesperado ao desserializar decimal: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        // Serializa como string (mantém consistência com API que envia string)
        writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}