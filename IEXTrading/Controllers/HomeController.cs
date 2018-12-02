using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using IEXTrading.Infrastructure.IEXTradingHandler;
using IEXTrading.Models;
using IEXTrading.Models.ViewModel;
using IEXTrading.DataAccess;
using Newtonsoft.Json;

namespace MVCTemplate.Controllers
{
    public class HomeController : Controller
    {
        public ApplicationDbContext dbContext;

        public HomeController(ApplicationDbContext context)
        {
            dbContext = context;
        }

        public IActionResult Index()
        {
            return View(getLastSavedQuotes());
        }

        /****
         * The Symbols action calls the GetSymbols method that returns a list of Companies.
         * This list of Companies is passed to the Symbols View.
        ****/
        public IActionResult Symbols()
        {
            //Set ViewBag variable first
            ViewBag.dbSucessComp = 0;
            IEXHandler webHandler = new IEXHandler();
            List<Company> companies = webHandler.GetSymbols();
            List<Quote> quotes = getTopQuotes();
            if(quotes.Count == 0)
            {
                Dictionary<String, Dictionary<String, Quote>> companiesQuotes = webHandler.GetQuotes(companies);
                TempData["Companies"] = JsonConvert.SerializeObject(companies);
                SaveCompanies();
                TempData["CompaniesQuote"] = JsonConvert.SerializeObject(companiesQuotes);
                SaveQuotes();
                quotes = getTopQuotes();
            }
            
            List<String> quoteSymbols = quotes.Select(q => q.symbol).ToList();
            companies = companies.Where(c => quoteSymbols.Contains(c.symbol)).ToList();
           

            //Save companies in TempData
            TempData["Companies"] = JsonConvert.SerializeObject(companies);
            

            return View(companies);
        }

        public IActionResult TopQuotes()
        {
            List<Quote> quotes = getTopQuotes();
            return View(quotes);
        }

        public IActionResult Quote(string symbol)
        {
            ViewBag.dbSuccessChart = 0;
            Quote quote = null;
            if (symbol != null)
            {
                IEXHandler webHandler = new IEXHandler();
                quote = webHandler.GetQuote(symbol);
            }

            CompaniesQuote companiesQuote = getCompaniesQuoteModel(quote);

            return View(companiesQuote);
        }

        public IActionResult Strategy() {
            return View();
        }

        /****
         * The Chart action calls the GetChart method that returns 1 year's equities for the passed symbol.
         * A ViewModel CompaniesEquities containing the list of companies, prices, volumes, avg price and volume.
         * This ViewModel is passed to the Chart view.
        ****/
        public IActionResult Chart(string symbol)
        {
            //Set ViewBag variable first
            ViewBag.dbSuccessChart = 0;
            List<Equity> equities = new List<Equity>();
            if (symbol != null)
            {
                IEXHandler webHandler = new IEXHandler();
                equities = webHandler.GetChart(symbol);
                equities = equities.OrderBy(c => c.date).ToList(); //Make sure the data is in ascending order of date.
            }

            CompaniesEquities companiesEquities = getCompaniesEquitiesModel(equities);

            return View(companiesEquities);
        }

        /****
         * The Refresh action calls the ClearTables method to delete records from a or all tables.
         * Count of current records for each table is passed to the Refresh View.
        ****/
        public IActionResult Refresh(string tableToDel)
        {
            ClearTables(tableToDel);
            Dictionary<string, int> tableCount = new Dictionary<string, int>();
            tableCount.Add("Companies", dbContext.Companies.Count());
            tableCount.Add("Charts", dbContext.Equities.Count());
            tableCount.Add("Quotes", dbContext.Quotes.Count());
            return View(tableCount);
        }

        /****
         * Saves the Symbols in database.
        ****/
        public IActionResult PopulateSymbols()
        {
            List<Company> companies = SaveCompanies();
            return View("Symbols", companies);
        }

        /****
         * Saves the equities in database.
        ****/
        public IActionResult SaveCharts(string symbol)
        {
            IEXHandler webHandler = new IEXHandler();
            List<Equity> equities = webHandler.GetChart(symbol);
            //List<Equity> equities = JsonConvert.DeserializeObject<List<Equity>>(TempData["Equities"].ToString());
            foreach (Equity equity in equities)
            {
                if (dbContext.Equities.Where(c => c.date.Equals(equity.date)).Count() == 0)
                {
                    dbContext.Equities.Add(equity);
                }
            }

            dbContext.SaveChanges();
            ViewBag.dbSuccessChart = 1;

            CompaniesEquities companiesEquities = getCompaniesEquitiesModel(equities);

            return View("Chart", companiesEquities);
        }

        public List<Company> SaveCompanies()
        {
            List<Company> companies = JsonConvert.DeserializeObject<List<Company>>(TempData["Companies"].ToString());
            foreach (Company company in companies)
            {
                //Database will give PK constraint violation error when trying to insert record with existing PK.
                //So add company only if it doesnt exist, check existence using symbol (PK)
                if (dbContext.Companies.Where(c => c.symbol.Equals(company.symbol)).Count() == 0)
                {
                    dbContext.Companies.Add(company);
                }
            }
            dbContext.SaveChanges();
            ViewBag.dbSuccessComp = 1;
            return companies;
        }

        public void SaveQuotes()
        {
            List<Company> companies = getCompanies();
            Dictionary<String, Dictionary<String, Quote>> companiesQuotes = JsonConvert.DeserializeObject<Dictionary<String, Dictionary<String, Quote>>>(TempData["CompaniesQuote"].ToString());
            foreach (var companyQuote in companiesQuotes)
            {
                //Database will give PK constraint violation error when trying to insert record with existing PK.
                //So add company only if it doesnt exist, check existence using symbol (PK)
                var quoteValue = companyQuote.Value?.FirstOrDefault().Value;
                if (dbContext.Quotes.Where(c => c.symbol.Equals(quoteValue.symbol)).Count() == 0)
                {
                    dbContext.Quotes.Add(quoteValue);
                }
            }
            dbContext.SaveChanges();
            ViewBag.dbSuccessComp = 1;
        }

        public IActionResult SaveQuote(string symbol)
        {
            IEXHandler webHandler = new IEXHandler();
            Quote quote = webHandler.GetQuote(symbol);
            //List<Equity> equities = JsonConvert.DeserializeObject<List<Equity>>(TempData["Equities"].ToString());
            
                if (dbContext.Quotes.Where(c => c.symbol.Equals(quote.symbol)).Count() == 0)
                {
                    quote.lastSaved = DateTime.Now;
                    dbContext.Quotes.Add(quote);
                }
            

            dbContext.SaveChanges();
            ViewBag.dbSuccessChart = 1;
            
            return View("Quote", getCompaniesQuoteModel(quote));
        }


        /****
         * Deletes the records from tables.
        ****/
        public void ClearTables(string tableToDel)
        {
            if ("all".Equals(tableToDel))
            {
                //First remove equities and then the companies
                dbContext.Equities.RemoveRange(dbContext.Equities);
                dbContext.Companies.RemoveRange(dbContext.Companies);
            }
            else if ("Companies".Equals(tableToDel))
            {
                //Remove only those that don't have Equity stored in the Equitites table
                dbContext.Companies.RemoveRange(dbContext.Companies
                                                         .Where(c => c.Equities.Count == 0)
                                                                      );
            }
            else if ("Charts".Equals(tableToDel))
            {
                dbContext.Equities.RemoveRange(dbContext.Equities);
            }
            dbContext.SaveChanges();
        }

        /****
         * Returns the ViewModel CompaniesEquities based on the data provided.
         ****/
        public CompaniesEquities getCompaniesEquitiesModel(List<Equity> equities)
        {
            List<Company> companies = getCompanies();

            if (equities.Count == 0)
            {
                return new CompaniesEquities(companies, null, "", "", "", 0, 0);
            }

            Equity current = equities.Last();
            string dates = string.Join(",", equities.Select(e => e.date));
            string prices = string.Join(",", equities.Select(e => e.high));
            string volumes = string.Join(",", equities.Select(e => e.volume / 1000000)); //Divide vol by million
            float avgprice = equities.Average(e => e.high);
            double avgvol = equities.Average(e => e.volume) / 1000000; //Divide volume by million
            return new CompaniesEquities(companies, equities.Last(), dates, prices, volumes, avgprice, avgvol);
        }

        public CompaniesQuote getCompaniesQuoteModel(Quote quote)
        {
            return new CompaniesQuote(getCompanies(), quote);
        }

        public List<Quote> getTopQuotes()
        {
            List<Quote> quotes = dbContext.Quotes.ToList();
            return quotes.OrderByDescending(a => a.annualPerformance).Take(5).ToList();
        }

        public List<Company> getCompanies()
        {
            List<Company> companies = dbContext.Companies.Take(1000).ToList();
            return companies;
        }

        public List<Quote> getLastSavedQuotes()
        {
            List<Quote> quotes = dbContext.Quotes.Where(a => a.lastSaved != null).OrderByDescending(a => a.lastSaved).Take(5).ToList();
            return quotes;
        }
    }
}
