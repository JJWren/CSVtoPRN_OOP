using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

namespace CSVtoPRN_OOP
{
    class Program
    {
        static void Main(string[] args)
        {
            // Get the user input filepath - put into string for splitting
            Console.WriteLine("Please enter in a valid directory for CSV import...\n\nExample - 'E:\\Wrenpo\\My Documents\\GitHub\\Repositories\\CSVtoPRN_OOP'): \n");
            string CSVDirectory = Console.ReadLine();
            if (CSVDirectory.Length - 1 != '\\')
            {
                CSVDirectory += '\\';
            }
            Console.WriteLine("Please enter the filename you wish to import...\nExample - 'good import.txt':\n");
            string CSVFilename = Console.ReadLine();
            string CSVFullPath = CSVDirectory + CSVFilename;

            // Display string contents
            Console.WriteLine($"CSV file: {CSVFullPath}");

            // Break the string down and output contents
            List<string> CSVAllLines = System.IO.File.ReadAllLines(CSVFullPath).ToList();

            // Lists for grouping each column into separate lists, maintaining order
            List<string> StoreNum = new List<string>();
            List<string> Date = new List<string>();
            List<string> Price = new List<string>();
            List<string> Fee = new List<string>();
            List<string> ID = new List<string>();
            List<PurchaseOrder> PurchaseOrders = new List<PurchaseOrder>();

            foreach (string row in CSVAllLines)
            {
                // Take row, put in list based on ','
                List<string> CSVRowAsList = row.Split(',').ToList();
                if (CSVRowAsList[CSVRowAsList.Count() - 2] == "ENDOFDATA")  // - 2 because last column is spaces ("    ") string on each row
                {
                    // Skip making list for ENDOFDATA rows
                    continue;
                }
                else
                {
                    // Trim index with spaces string
                    if (CSVRowAsList.Any())   // prevent IndexOutOfRangeException for empty list
                    {
                        CSVRowAsList.RemoveAt(CSVRowAsList.Count - 1);
                    }

                    // // TROUBLESHOOTING: Print List
                    // PrintFormattedList(CSVRowAsList);

                    // Input each row's items into category maintaining idx integrity - excluding irrel. items
                    for (int i = 0; i < CSVRowAsList.Count(); i++)
                    {
                        switch (i)
                        {
                            case 3:
                                StoreNum.Add(CSVRowAsList[i].TrimStart(new char[] { '0' }));
                                break;
                            case 4:
                                Date.Add(CSVRowAsList[i]);
                                break;
                            case 5:
                                Price.Add(CSVRowAsList[i].TrimStart(new char[] { '0' }));
                                break;
                            case 6:
                                Fee.Add(CSVRowAsList[i].TrimStart(new char[] { '0' }));
                                break;
                            case 8:
                                ID.Add(CSVRowAsList[i]);
                                break;
                        }
                    }
                }
            }

            // Create List of Orders (class)
            for (int i = 0; i < StoreNum.Count(); i++)
            {
                int storenum = Int32.Parse((StoreNum[i]));
                double price = double.Parse(Price[i]);
                double fee = double.Parse(Fee[i]);

                if (price > 0)
                {
                    // PurchaseOrder(string id, string date, double price, double fee)
                    PurchaseOrder order = new PurchaseOrder(ID[i], Date[i], storenum, price, fee);
                    PurchaseOrders.Add(order);
                }
            }

            // // TROUBLESHOOTING: Checking ListOfOrder data
            // foreach (PurchaseOrder order in PurchaseOrders)
            // {
            //     CultureInfo provider = CultureInfo.InvariantCulture;
            //     string fDate = DateTime.ParseExact(order.Date, "yyMMdd", provider).ToString("MM/dd/yy");
            //     // testing to make sure orders are in list of orders
            //     Console.WriteLine($"ID: {order.ID}\tDate: {fDate}\tStoreNum: {order.StoreNum}\tPrice: {order.Price}\tFee: {order.Fee}\tAlcoholic?: {order.IsAlcoholic.ToString()}");
            // }

            // Getting Unique Dates and Unique Store Numbers
            List<int> UniqueDates = new List<int>();
            List<int> UniqueStores = new List<int>();

            foreach (PurchaseOrder order in PurchaseOrders)
            {
                if (!UniqueDates.Contains(Int32.Parse(order.Date)))
                {
                    UniqueDates.Add(Int32.Parse(order.Date));
                }
                if (!UniqueStores.Contains(order.StoreNum))
                {
                    UniqueStores.Add(order.StoreNum);
                }
            }
            // Order the dates from most recent to oldest
            UniqueDates.Sort();
            UniqueDates.Reverse();

            // // TROUBLESHOOTING: Check order of dates
            // foreach (int date in UniqueDates)
            // {
            //     Console.WriteLine(date);
            // }

            // Final String for putting into new file:
            string FinalString = "";

            // Match Unique Dates to PO for grouping
            foreach (int date in UniqueDates)
            {
                // date as string
                string StrDate = date.ToString();

                // Query for this date where a given store has POs
                List<Store> StoresWithAlcSales = new List<Store>();
                List<Store> StoresWithAnySales = new List<Store>();
                foreach (int store in UniqueStores)
                {
                    List<PurchaseOrder> AllStoreOrdersForDate = new List<PurchaseOrder>();
                    List<PurchaseOrder> AllAlcStoreOrdersForDate = new List<PurchaseOrder>();
                    foreach (PurchaseOrder po in PurchaseOrders)
                    {
                        if (po.StoreNum == store && po.Date == StrDate)
                        {
                            AllStoreOrdersForDate.Add(po);
                            if (po.IsAlcoholic)
                            {
                                AllAlcStoreOrdersForDate.Add(po);
                            }
                        }
                    }
                    if (AllStoreOrdersForDate.Count() > 0)
                    {
                        Store StoreWithSales = new Store(store, AllStoreOrdersForDate, AllAlcStoreOrdersForDate);
                        StoresWithAnySales.Add(StoreWithSales);
                        if (AllAlcStoreOrdersForDate.Count() > 0)
                        {
                            StoresWithAlcSales.Add(StoreWithSales);
                        }
                    }
                }

                // START OF GATHERING FINAL STRING TO PUT IN NEW FILE
                if (StoresWithAnySales.Count() > 0)
                {
                    if (StoresWithAlcSales.Count() > 0)
                    {
                        FinalString += SectionHeader(StrDate, true);
                        double DateAlcTotal = 0;
                        foreach (Store store in StoresWithAlcSales)
                        {
                            double AlcSaleTotal = 0;
                            foreach (PurchaseOrder po in store.AlcPOs)
                            {
                                AlcSaleTotal += po.Price;
                                DateAlcTotal -= po.Price;
                            }
                            FinalString += $"FA:\t1\t{store.StoreNum}\t99\t2153\t{AlcSaleTotal}\n";
                        }
                        FinalString += $"FA:\t1\t90\t99\t2146\t{DateAlcTotal}\nEND:\n";
                    }
                    FinalString += SectionHeader(StrDate, false);
                    // These values are constant - as far as I am aware
                    double AlcVal = 0.14;
                    double OtherVal = -0.85;
                    double AllStoreSums = 0;
                    foreach (Store store in StoresWithAnySales)
                    {
                        int SumOfAlcPOs = 0;
                        int SumOfOtherPOs = 0;
                        foreach (PurchaseOrder po in store.POs)
                        {
                            if (po.IsAlcoholic)
                            {
                                SumOfAlcPOs += 1;
                            }
                            else
                            {
                                SumOfOtherPOs += 1;
                            }
                        }
                        double AlcSumTimesAlcVal = Math.Round((SumOfAlcPOs * AlcVal), 2, MidpointRounding.AwayFromZero);
                        double OtherSumTimesOtherVal = Math.Round((SumOfOtherPOs * OtherVal), 2, MidpointRounding.AwayFromZero);
                        double TotalSum = AlcSumTimesAlcVal + Math.Round(OtherSumTimesOtherVal, 2, MidpointRounding.AwayFromZero);
                        FinalString += $"FA:\t1\t{store.StoreNum}\t99\t4520\t{TotalSum}\n";
                        AllStoreSums += TotalSum;
                    }
                    FinalString += $"FA:\t1\t90\t99\t2146\t{AllStoreSums}\nEND:\n";
                }
                // END OF FINAL STRING COMBINATION
            }

            // REIMPLEMENTED THE FILE CREATION
            // string CurrDT = DateTime.Now.ToString("yyyyMMdd");

            // string NewFilePath = $"{CSVDirectory}ConvertedCSV{CurrDT}.prn";
            // if (!File.Exists(NewFilePath))
            // {
            //     // Create a file to write to.
            //     using (StreamWriter sw = File.CreateText(NewFilePath))
            //     {
            //         sw.WriteLine($"{FinalString}");
            //     }
            // }

            // Create new prn file from converted string
            // Formatted name
            string NewFilePath = $"{CSVDirectory}ConvertedCSV.prn";
            // Fixes formatted name if the file already exists >> (Directory\ConvertCSV.prn will become Directory\ConvertedCSV (1).prn, etc)
            string FinalFilePath = GetUniqueFilename(NewFilePath);
            // Create the file and use the FinalString for the text
            using (StreamWriter sw = File.CreateText(FinalFilePath))
            {
                sw.WriteLine($"{FinalString}");
            }
            // END OF MAIN
        }

        static void PrintFormattedList(List<string> list)
        {
            for (int i = 0; i < list.Count(); i++)
            {
                if (i == 0)
                {
                    Console.Write($"[ {list[i]},");
                }
                else if (i < list.Count() - 1)
                {
                    Console.Write($" {list[i]},");
                }
                else
                {
                    Console.WriteLine($" {list[i]}]");
                }
            }
        }

        static string SectionHeader(string date, bool alcohol)
        {
            CultureInfo provider = CultureInfo.InvariantCulture;
            string FormatDate = DateTime.ParseExact(date, "yyMMdd", provider).ToString("MM/dd/yy");
            string Header = $"0001\n0001\n0090\nJE\n{FormatDate}\n";
            if (alcohol == true)
            {
                Header += $"APPLY BEER & WINE P.O.\nN\n  0\n";
            }
            else
            {
                Header += $"APPLY MONEY ORDER COMMISION\nN\n  0\n";
            }
            return Header;
        }

        static string GetUniqueFilename(string fullPath)
        {
            if (!Path.IsPathRooted(fullPath))
                fullPath = Path.GetFullPath(fullPath);
            if (File.Exists(fullPath))
            {
                String filename = Path.GetFileName(fullPath);
                String path = fullPath.Substring(0, fullPath.Length - filename.Length);
                String filenameWOExt = Path.GetFileNameWithoutExtension(fullPath);
                String ext = Path.GetExtension(fullPath);
                int n = 1;
                do
                {
                    fullPath = Path.Combine(path, String.Format("{0} ({1}){2}", filenameWOExt, (n++), ext));
                }
                while (File.Exists(fullPath));
            }
            return fullPath;
        }
    }

    class PurchaseOrder
    {
        public string ID;
        public string Date;
        public int StoreNum;
        public double Price;
        public double Fee;
        public bool IsAlcoholic;

        public PurchaseOrder(string id, string date, int storenum, double price, double fee)
        {
            this.ID = id;
            this.Date = date;
            this.StoreNum = storenum;
            this.Price = price;
            this.Fee = fee;

            if (this.Price > 0 && fee == 0)
            {
                this.IsAlcoholic = true;
            }
            else
            {
                this.IsAlcoholic = false;
            }
        }
    }

    class Store
    {
        public int StoreNum;
        public List<PurchaseOrder> POs = new List<PurchaseOrder>();
        public List<PurchaseOrder> AlcPOs = new List<PurchaseOrder>();

        public Store(int storenum, List<PurchaseOrder> purchaseorders, List<PurchaseOrder> alcpurchaseorders)
        {
            this.StoreNum = storenum;
            this.POs.AddRange(purchaseorders);
            this.AlcPOs.AddRange(alcpurchaseorders);
        }
    }
}
