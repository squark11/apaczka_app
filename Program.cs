using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


public class Program
{
    private const string AppId = "2209997_33awUikp6m4ilBkg0rM4hyI7";
    private const string AppSecret = "zrzvfmpq2mmd1jivuwgjnacbdaj3fnpa";
    private const string ApiUrl = "https://www.apaczka.pl/api/v2/order_valuation/";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Przygotowywanie zapytania o wycene (finalna wersja)...");

        var orderData = new Order
        {
            Sender = new Address { Name = "Firma Testowa Nadawca", ContactPerson = "Jan Nowak", Email = "test@nadawca.pl", Phone = "500100200", Line1 = "ul. Prosta 1", PostalCode = "00-001", City = "Warszawa", CountryCode = "PL" },
            Receiver = new Address { Name = "Firma Testowa Odbiorca", ContactPerson = "Anna Kowalska", Email = "test@odbiorca.pl", Phone = "600700800", Line1 = "ul. Krzywa 123", PostalCode = "30-002", City = "Krakow", CountryCode = "PL" },
            Shipments = new List<Shipment> { new Shipment { ShipmentTypeCode = "PARCEL", Weight = 5, Width = 30, Height = 20, Length = 40, Content = "Artykuly testowe" } }
        };

        var requestPayload = new { order = orderData };
        string requestJson = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        long expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (25 * 60);
        string route = new Uri(ApiUrl).AbsolutePath;


        string stringToSign = $"{AppId}:{route}:{requestJson}:{expires}";
        string signature = GenerateSignature(stringToSign, AppSecret);

        var formData = new Dictionary<string, string>
        {
            { "app_id", AppId },
            { "request", requestJson },
            { "expires", expires.ToString() },
            { "signature", signature }
        };

        await SendRequest(formData);
    }

    private static async Task SendRequest(Dictionary<string, string> formData)
    {
        Console.WriteLine("\n--- DANE FORMULARZA WYSYLANE DO API ---");
        foreach (var entry in formData)
        {
            Console.WriteLine($"Klucz: {entry.Key}, Wartosc: {(entry.Value.Length > 70 ? entry.Value.Substring(0, 70) + "..." : entry.Value)}");
        }
        Console.WriteLine("----------------------------------------\n");

        using (var httpClient = new HttpClient())
        {
            try
            {
                var httpContent = new FormUrlEncodedContent(formData);
                var response = await httpClient.PostAsync(ApiUrl, httpContent);
                string responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine("--- OTRZYMANO ODPOWIEDZ Z SERWERA ---");
                Console.WriteLine(responseBody);
                Console.WriteLine("--------------------------------------");

                if (response.IsSuccessStatusCode && responseBody.Contains("\"status\":200"))
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<ValuationResponse>>(responseBody);
                    Console.WriteLine("\n--- ✅ SUKCES! OTO WYCENA ---");
                    foreach (var entry in apiResponse.Response.PriceTable)
                    {
                        decimal priceGross = entry.Value.PriceGross / 100.0m;
                        Console.WriteLine($"ID Serwisu: {entry.Key} -> Cena brutto: {priceGross:C}");
                    }
                }
                else
                {
                    Console.WriteLine($"\nBłąd w odpowiedzi API. Sprawdź komunikat powyżej.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nWystapil krytyczny blad: {ex.Message}");
            }
        }
    }

    private static string GenerateSignature(string stringToSign, string key)
    {
        var encoding = new UTF8Encoding();
        byte[] keyByte = encoding.GetBytes(key);
        byte[] messageBytes = encoding.GetBytes(stringToSign);
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            return BitConverter.ToString(hashmessage).Replace("-", "").ToLower();
        }
    }
}

public class Address { public string Name { get; set; } public string ContactPerson { get; set; } public string Email { get; set; } public string Phone { get; set; } public string Line1 { get; set; } public string PostalCode { get; set; } public string City { get; set; } public string CountryCode { get; set; } }
public class Shipment { public string ShipmentTypeCode { get; set; } public int Weight { get; set; } public int Width { get; set; } public int Height { get; set; } public int Length { get; set; } public string Content { get; set; } }
public class Order { public Address Sender { get; set; } public Address Receiver { get; set; } public List<Shipment> Shipments { get; set; } }
public class ApiResponse<T> { [JsonPropertyName("status")] public int Status { get; set; } [JsonPropertyName("message")] public string Message { get; set; } [JsonPropertyName("response")] public T Response { get; set; } }
public class ValuationResponse { [JsonPropertyName("price_table")] public Dictionary<string, PriceInfo> PriceTable { get; set; } }
public class PriceInfo { [JsonPropertyName("price_gross")] public int PriceGross { get; set; } }