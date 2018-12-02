using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using IEXTrading.Models;
using IEXTrading.Models.ViewModel;
using Newtonsoft.Json;

namespace IEXTrading.Infrastructure.IEXTradingHandler
{
    public class IEXHandler
    {
        static string BASE_URL = "https://api.iextrading.com/1.0/"; //This is the base URL, method specific URL is appended to this.
        HttpClient httpClient;

        public IEXHandler()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /****
         * Calls the IEX reference API to get the list of symbols. 
        ****/
        public List<Company> GetSymbols()
        {
            string IEXTrading_API_PATH = BASE_URL + "ref-data/symbols";
            string companyList = "";

            List<Company> companies = null;

            httpClient.BaseAddress = new Uri(IEXTrading_API_PATH);
            HttpResponseMessage response = httpClient.GetAsync(IEXTrading_API_PATH).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                companyList = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }

            if (!companyList.Equals(""))
            {
                companies = JsonConvert.DeserializeObject<List<Company>>(companyList);
                companies = companies.Where(c => c.isEnabled && c.type != "N/A").ToList();
                //companies = companies.GetRange(0, 9);
            }
            return companies;
        }

        public Dictionary<String,Dictionary<String,Quote>> GetQuotes(List<Company> companies)
        {
            string symbols = "";
            int skipCount = 0;
            Dictionary<String, Dictionary<String, Quote>> companiesQuote = new Dictionary<String, Dictionary<String, Quote>>();
            while (true)
            {
                if (skipCount <= companies.Count())
                {
                    symbols = string.Join(",", companies.Skip(skipCount).Take(100).Select(c => c.symbol).ToArray());

                    string IEXTrading_API_PATH = BASE_URL + "stock/market/batch?symbols=" + symbols + "&types=quote";
                    string responseData = "";
                    Dictionary<string,Dictionary<String,Quote>> quotes = null;
                    //Dictionary<string, Quote> quotesDict = new Dictionary<string, Quote>();
                    HttpResponseMessage response = httpClient.GetAsync(IEXTrading_API_PATH).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        responseData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    }

                    if (!string.IsNullOrEmpty(responseData))
                    {
                        quotes = JsonConvert.DeserializeObject<Dictionary<String,Dictionary<String, Quote>>>(responseData);
                        quotes = quotes.Where(c => c.Value?.FirstOrDefault().Value?.week52High > c.Value?.FirstOrDefault().Value?.week52Low && c.Value?.FirstOrDefault().Value?.companyName.Length > 0).ToDictionary(x => x.Key, y => y.Value);
                        skipCount += 100;
                        companiesQuote = companiesQuote.Concat(quotes).ToDictionary(x => x.Key, y => y.Value);
                    }
                }
                else
                {
                    break;
                }
            }

            if (companiesQuote != null)
            {
                foreach (var companyQuote in companiesQuote)
                {
                    var quoteValue = companyQuote.Value?.FirstOrDefault().Value;
                    quoteValue.annualPerformance = (quoteValue?.close - quoteValue?.week52Low) / (quoteValue?.week52High - quoteValue?.week52Low);
                }
            }
            return companiesQuote.OrderByDescending(a => a.Value?.FirstOrDefault().Value?.annualPerformance).Take(5).ToDictionary(x=>x.Key, y=>y.Value);
        }

        public Quote GetQuote(string symbol)
        {
            string IEXTrading_API_PATH = BASE_URL + "stock/" + symbol + "/quote";
            string responseData = "";
            Quote quote = new Quote();

            HttpResponseMessage response = httpClient.GetAsync(IEXTrading_API_PATH).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                responseData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }

            if (!string.IsNullOrEmpty(responseData))
            {
                quote = JsonConvert.DeserializeObject<Quote>(responseData);
            }

            return quote;
        }

        /****
         * Calls the IEX stock API to get 1 year's chart for the supplied symbol. 
        ****/
        public List<Equity> GetChart(string symbol)
        {
            //Using the format method.
            //string IEXTrading_API_PATH = BASE_URL + "stock/{0}/batch?types=chart&range=1y";
            //IEXTrading_API_PATH = string.Format(IEXTrading_API_PATH, symbol);

            string IEXTrading_API_PATH = BASE_URL + "stock/" + symbol + "/batch?types=chart&range=1y";

            string charts = "";
            List<Equity> Equities = new List<Equity>();
            httpClient.BaseAddress = new Uri(IEXTrading_API_PATH);
            HttpResponseMessage response = httpClient.GetAsync(IEXTrading_API_PATH).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                charts = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            if (!charts.Equals(""))
            {
                ChartRoot root = JsonConvert.DeserializeObject<ChartRoot>(charts, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Equities = root.chart.ToList();
            }
            //make sure to add the symbol the chart
            foreach (Equity Equity in Equities)
            {
                Equity.symbol = symbol;
            }

            return Equities;
        }
    }
}
