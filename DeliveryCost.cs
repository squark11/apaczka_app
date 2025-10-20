using Comarch.eShop.ISync;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ApaczkaDeliveryCost
{
    public class DeliveryCost : Worker
    {
        private const string AppId = "2209997_33awUikp6m4ilBkg0rM4hyI7";
        private const string AppSecret = "zrzvfmpq2mmd1jivuwgjnacbdaj3fnpa";
        private const string ApiUrl = "https://www.apaczka.pl/api/v2/order_valuation/";

        public override Task<Msg> Execute(Msg msgOuter)
        {
            return Task.Factory.StartNew(() =>
            {
                var req = JsonConvert.DeserializeObject<ExternalDeliveryCostQuery2>(msgOuter.Message);

                decimal totalCost = 0;

                foreach (var set in req.Sets)
                {
                    foreach (var element in set.Elements)
                    {
                        var order = new
                        {
                            order = new
                            {
                                shipment = new[]
                                {
                                    new
                                    {
                                        shipment_type_code = "PACZKA",
                                        weight = element.Weight,
                                        dimension1 = element.Length,
                                        dimension2 = element.Width,
                                        dimension3 = element.Height,
                                        content = "Produkt z eShop"
                                    }
                                }
                            }
                        };

                        string requestJson = JsonConvert.SerializeObject(order);
                        long expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (25 * 60);
                        string stringToSign = $"{AppId}:order_valuation/:{requestJson}:{expires}";
                        string signature = GenerateSignature(stringToSign, AppSecret);

                        var formData = new Dictionary<string, string>
                        {
                            { "app_id", AppId },
                            { "request", requestJson },
                            { "expires", expires.ToString() },
                            { "signature", signature }
                        };

                        // Wywołanie HTTP synchronously w .NET Framework
                        decimal elementCost = GetPriceFromApaczka(formData);
                        totalCost += elementCost;
                    }
                }

                msgOuter.Response = JsonConvert.SerializeObject(
                    req.MethodId.Select(m => new ExternalDeliveryCostCl2
                    {
                        MethodId = m,
                        Cost = totalCost,
                        FreePayment = false,
                        CalculationId = "apaczka-worker"
                    })
                );

                return msgOuter;
            });
        }

        private static decimal GetPriceFromApaczka(Dictionary<string, string> formData)
        {
            using (var httpClient = new HttpClient())
            {
                var httpContent = new FormUrlEncodedContent(formData);
                var response = httpClient.PostAsync(ApiUrl, httpContent).Result;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                try
                {
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<ValuationResponse>>(responseBody);
                    if (apiResponse.Status == 200 && apiResponse.Response?.PriceTable != null && apiResponse.Response.PriceTable.Count > 0)
                    {
                        var firstPriceGross = apiResponse.Response.PriceTable.Values.First().PriceGross;
                        decimal gross;
                        if (decimal.TryParse(firstPriceGross, out gross))
                            return gross / 100m; // grosze -> zł
                    }
                }
                catch
                {
                    // błąd -> 0
                }
            }
            return 0;
        }

        private static string GenerateSignature(string stringToSign, string key)
        {
            using (var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
                return BitConverter.ToString(hashmessage).Replace("-", "").ToLower();
            }
        }
    }

    #region MODELS
    public class ExternalDeliveryCostQuery2
    {
        public int[] MethodId;
        public string Country;
        public string City;
        public string ZipCode;
        public decimal TotalValue;
        public decimal SubtotalValue;
        public List<ExternalDeliveryCostSetQuery2> Sets;
    }

    public class ExternalDeliveryCostSetQuery2
    {
        public int Id;
        public int? BundleId;
        public decimal? CustomDeliveryTotalValue;
        public List<ExternalDeliveryCostElementQuery2> Elements;
    }

    public class ExternalDeliveryCostElementQuery2
    {
        public int? Id;
        public int EshopProductId;
        public decimal Quantity;
        public short? MapType;
        public string MapForeignId;
        public bool? BundleGratis;
        public bool? BundleAddHeaderDiscount;
        public string ExtId;

        public decimal Weight;
        public decimal Length;
        public decimal Width;
        public decimal Height;
    }

    public class ExternalDeliveryCostCl2
    {
        public int MethodId;
        public decimal Cost;
        public bool FreePayment;
        public string CalculationId;
    }

    public class ApiResponse<T>
    {
        public int Status { get; set; }
        public string Message { get; set; }
        public T Response { get; set; }
    }

    public class ValuationResponse
    {
        public Dictionary<string, PriceInfo> PriceTable { get; set; }
    }

    public class PriceInfo
    {
        public string Price { get; set; }
        public string PriceGross { get; set; }
    }
    #endregion
}
