using System;
using System.IO;
using System.Text.Json;

namespace DTCL.JsonParser
{
    public class JsonParser<T>
    {
        // Method to serialize an object to a JSON string
        public string Serialize(T obj)
        {
            try
            {
                return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Serialization error: {ex.Message}");
                return string.Empty;
            }
        }

        // Method to deserialize a JSON string to an object of type T
        public T Deserialize(string filePath)
        {
            try
            {
                var jsonString = File.ReadAllText(filePath);

                var obj = JsonSerializer.Deserialize<T>(jsonString);

                return JsonSerializer.Deserialize<T>(jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deserialization error: {ex.Message}");
                return default(T);
            }
        }
    }
}
