using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace IEXTrading.Models.ViewModel
{
    public class CompaniesQuoteRoot {
        public Dictionary<string, Dictionary<string, Quote>> companiesQuoteRoot { get; set; }
        public List<Company> Companies { get; set; }

        public CompaniesQuoteRoot(List<Company> companies, Dictionary<string, Dictionary<string, Quote>> quoteRoot) {
            Companies = companies;
            companiesQuoteRoot = quoteRoot;
        }
    }

    public class CompaniesQuote
    {
        public List<Company> companies { get; set; }
        public Quote quote { get; set; }

        public CompaniesQuote(List<Company> companies, Quote quote)
        {
            this.companies = companies;
            this.quote = quote;
        }
    }

}
