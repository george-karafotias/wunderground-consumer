using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WundergroundHtmlParser
{
    class Program
    {
        private static int TIME_COLUMN = 0;
        private static int TEMP_COLUMN = 1;
        private static int DEWPOINT_COLUMN = 2;
        private static int HUMIDITY_COLUMN = 3;
        private static int PRESSURE_COLUMN = 4;
        private static int VISIBILITY_COLUMN = 5;
        private static int WINDDIR_COLUMN = 6;
        private static int WINDSPEED_COLUMN = 7;
        private static int GUSTSPEED_COLUMN = 8;
        private static int PRECIP_COLUMN = 9;
        private static int EVENTS_COLUMN = 10;
        private static int CONDITIONS_COLUMN = 11;
        private static int WINDCHILL_COLUMN = 12;

        private static string LINESEP = "<br />";

        private static Dictionary<int, int> columnMapping;
        private static Dictionary<int, List<string>> columnKeywords;

        static void Main(string[] args)
        {
            if (args == null || args.Length != 2)
            {
                Console.WriteLine("Usage airportCode year");
                return;
            }

            string airportCode = args[0];
            string year = args[1];

            AddDefaultColumnMapping();
            AddColumnKeywords();
            DownloadAirportYear(airportCode, year);
        }

        private static void AddDefaultColumnMapping()
        {
            columnMapping = new Dictionary<int, int>();
            columnMapping.Add(TIME_COLUMN, TIME_COLUMN);
            columnMapping.Add(TEMP_COLUMN, TEMP_COLUMN);
            columnMapping.Add(DEWPOINT_COLUMN, DEWPOINT_COLUMN);
            columnMapping.Add(HUMIDITY_COLUMN, HUMIDITY_COLUMN);
            columnMapping.Add(PRESSURE_COLUMN, PRESSURE_COLUMN);
            columnMapping.Add(VISIBILITY_COLUMN, VISIBILITY_COLUMN);
            columnMapping.Add(WINDDIR_COLUMN, WINDDIR_COLUMN);
            columnMapping.Add(WINDSPEED_COLUMN, WINDSPEED_COLUMN);
            columnMapping.Add(GUSTSPEED_COLUMN, GUSTSPEED_COLUMN);
            columnMapping.Add(PRECIP_COLUMN, PRECIP_COLUMN);
            columnMapping.Add(EVENTS_COLUMN, EVENTS_COLUMN);
            columnMapping.Add(CONDITIONS_COLUMN, CONDITIONS_COLUMN);
            columnMapping.Add(WINDCHILL_COLUMN, -1);
        }

        private static void AddColumnKeywords()
        {
            columnKeywords = new Dictionary<int, List<string>>();
            columnKeywords.Add(TIME_COLUMN, new List<string> { "Time (EET)", "Time (EEST)" });
            columnKeywords.Add(TEMP_COLUMN, new List<string> { "Temp." });
            columnKeywords.Add(WINDCHILL_COLUMN, new List<string> { "Windchill" });
            columnKeywords.Add(DEWPOINT_COLUMN, new List<string> { "Dew Point" });
            columnKeywords.Add(HUMIDITY_COLUMN, new List<string> { "Humidity" });
            columnKeywords.Add(PRESSURE_COLUMN, new List<string> { "Pressure" });
            columnKeywords.Add(VISIBILITY_COLUMN, new List<string> { "Visibility" });
            columnKeywords.Add(WINDDIR_COLUMN, new List<string> { "Wind Dir" });
            columnKeywords.Add(WINDSPEED_COLUMN, new List<string> { "Wind Speed" });
            columnKeywords.Add(GUSTSPEED_COLUMN, new List<string> { "Gust Speed" });
            columnKeywords.Add(PRECIP_COLUMN, new List<string> { "Precip" });
            columnKeywords.Add(EVENTS_COLUMN, new List<string> { "Events" });
            columnKeywords.Add(CONDITIONS_COLUMN, new List<string> { "Conditions" });
        }

        private static string ConstructDayUrl(string airportCode, DateTime day)
        {
            StringBuilder dayUrl = new StringBuilder();
            dayUrl.Append("https://www.wunderground.com/history/airport/");
            dayUrl.Append(airportCode);
            dayUrl.Append("/");
            dayUrl.Append(day.ToString("yyyy/MM/dd"));
            dayUrl.Append("/");
            dayUrl.Append("DailyHistory.html");

            return dayUrl.ToString();
        }

        public static void DownloadAirportYear(string airportCode, string year)
        {
            DateTime startDay = DateTime.Parse("1/1/" + year);
            DateTime endDay = DateTime.Parse("31/12/" + year);

            List<string> data = new List<string>();

            for (DateTime day = startDay; day <= endDay; day = day.AddDays(1))
            {
                Console.WriteLine("Downloading " + airportCode + " data for " + day.ToString("dd/MM/yyyy..."));

                try
                {
                    using (WebClient client = new WebClient())
                    {
                        string dayUrl = ConstructDayUrl(airportCode, day);
                        string htmlPage = client.DownloadString(dayUrl);
                        string dayData = ParseDay(htmlPage);
                        data.Add(dayData);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            WriteData(airportCode, year, data);
        }

        private static void WriteData(string airportCode, string year, List<string> data)
        {
            try
            {
                TextWriter tw = new StreamWriter(airportCode + year + ".txt");

                foreach (String s in data)
                    tw.WriteLine(s);

                tw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static string ParseDay(string htmlPage)
        {
            StringBuilder returnValue = new StringBuilder();

            try
            {
                WebUtility.HtmlDecode(htmlPage);
                HtmlDocument htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(htmlPage);

                HtmlNode dataTable = htmlDocument.DocumentNode.Descendants().Where
                    (x => (x.Name == "table" && x.Attributes["id"] != null && x.Attributes["id"].Value.Contains("obsTable"))).ToList()[0];

                UpdateColumnMapping(dataTable);

                returnValue.Append(ExtractHeaderRow(dataTable));
                returnValue.Append(LINESEP);
                returnValue.Append(ExtractDataRows(dataTable));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return returnValue.ToString();
        }

        private static void UpdateColumnMapping(HtmlNode dataTable)
        {
            List<HtmlNode> headerColumns = ExtractHeaderColumns(dataTable);

            List<string> headerColumnTexts = new List<string>();
            for (int i=0; i<headerColumns.Count; i++)
            {
                headerColumnTexts.Add(headerColumns[i].InnerText);
            }

            foreach (KeyValuePair<int, List<string>> entry in columnKeywords)
            {
                columnMapping[entry.Key] = FindFirstInSecond(entry.Value, headerColumnTexts);
            }
        }

        private static int FindFirstInSecond(List<string> list1, List<string> list2)
        {
            foreach (string s1 in list1)
            {
                for (int i=0; i<list2.Count; i++)
                {
                    if (string.Equals(s1, list2[i], StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return -1;
        }

        private static List<HtmlNode> ExtractHeaderColumns(HtmlNode dataTable)
        {
            return dataTable.Descendants("thead").ToList()[0].Descendants("tr").ToList()[0].Descendants("th").ToList();
        }

        private static string ExtractHeaderRow(HtmlNode dataTable)
        {
            List<string> headerColumnsText = new List<string>();
            List<HtmlNode> headerColumns = ExtractHeaderColumns(dataTable);

            foreach (KeyValuePair<int, int> cm in columnMapping)
            {
                if (cm.Value >= 0 && cm.Key != WINDCHILL_COLUMN)
                    headerColumnsText.Add(headerColumns[cm.Value].InnerText);
            }

            return string.Join(",", headerColumnsText.ToArray());
        }

        private static string ExtractDataRows(HtmlNode dataTable)
        {
            StringBuilder returnValue = new StringBuilder();

            var dataRows = dataTable.Descendants("tbody").ToList()[0].Descendants("tr").ToList();

            foreach (var dataRow in dataRows)
            {
                var dataColumns = dataRow.Descendants("td").ToList();

                List<string> rowData = new List<string>();

                foreach (KeyValuePair<int, int> cm in columnMapping)
                {
                    int cmIndex = cm.Value;
                    
                    if (cmIndex >= 0 && cm.Key != WINDCHILL_COLUMN)
                    {
                        List<HtmlNode> valueSpan = dataColumns[cmIndex].Descendants().Where
                            (x => (x.Name == "span" && x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("wx-value"))).ToList();

                        string columnData = "";

                        if (valueSpan != null && valueSpan.Count == 1 && valueSpan[0] != null)
                            columnData = valueSpan[0].InnerText;
                        else
                            columnData = dataColumns[cmIndex].InnerText;

                        columnData = PreProcessColumnData(columnData, cmIndex);

                        rowData.Add(columnData);
                    }
                }

                returnValue.Append(string.Join(",", rowData.ToArray()));
                returnValue.Append(LINESEP);
            }

            return returnValue.ToString();
        }

        private static string PreProcessColumnData(string data, int index)
        {
            data = data.Trim();
            data = Regex.Replace(data, @"\t|\n|\r|", "");
            data = data.Replace("&nbsp;", " ");
            data = data.Replace(",", " ");

            if (index == WINDSPEED_COLUMN && data.Any(char.IsDigit))
            {
                data = LeaveOnlyNumeric(data);
            }

            if (index == TEMP_COLUMN || index == DEWPOINT_COLUMN || index == HUMIDITY_COLUMN || index == PRESSURE_COLUMN || index == VISIBILITY_COLUMN || index == GUSTSPEED_COLUMN)
            {
                data = LeaveOnlyNumeric(data);
            }

            data = data.Trim();

            return data;
        }

        private static string LeaveOnlyNumeric(string data)
        {
            string result = string.Empty;

            foreach (var c in data)
            {
                int ascii = (int)c;
                if ((ascii >= 48 && ascii <= 57) || ascii == 44 || ascii == 45 || ascii == 46)
                    result += c;
                else
                    break;
            }

            return result;
        }
    }
}
