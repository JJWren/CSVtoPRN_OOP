using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

/*
 TITLE:	 Money Orders CSV to PRN File Converter
 AUTHOR: Joshua Wren
 DESCR:	 This program's purpose is to take moneyorders.csv and convert it into gltrnimprt.prn.
		 The executable will be located directly in the folder the user keeps the csv file in.
		 The program includes logic for calculating some totaled values for each store per day for both total money orders and specific data relevant for alcohol money order totals.
*/

namespace CSVtoPRN_OOP
{
	class Program
	{
		static void Main(string[] args)
		{
            string fileRoot = "C:\\westernunion";
            string csvFilename = "moneyorders.csv";
			//string csvFilename = args[0];	//Argument located in shortcut exe (Example: "moneyorders.csv")
			string csvFullPath = Path.Combine(fileRoot, csvFilename);

            try
            {
                //Break the string down and output contents
		        List<string> csvAllLines = File.ReadAllLines(csvFullPath).ToList();

                //List of all PurchaseOrder instances
                List<PurchaseOrder> PurchaseOrders = new List<PurchaseOrder>();

                foreach (string row in csvAllLines)
                {
                    //Take row, put in list based on ','
                    List<string> csvRowAsList = row.Split(',',StringSplitOptions.RemoveEmptyEntries).ToList();

                    //Skip making list for ENDOFDATA rows
                    if (csvRowAsList[csvRowAsList.Count() - 2] != "ENDOFDATA")
                    {
                        //Input each row's items into category maintaining idx integrity - excluding irrel. items
                        //Create PurchaseOrder instances based on row csvRowAsList indices
                        PurchaseOrder PO = new PurchaseOrder();
                        for (int i = 0; i < csvRowAsList.Count(); i++)
                        {
                            switch (i)
                            {
                                case 3:
                                    var storeNum = Int32.Parse((csvRowAsList[i]));
                                    PO.StoreNum = storeNum;
                                    break;
                                case 4:
                                    PO.Date = csvRowAsList[i];
                                    break;
                                case 5:
                                    var price = decimal.Parse(csvRowAsList[i]);
                                    PO.Price = price;
                                    break;
                                case 6:
                                    var fee = decimal.Parse(csvRowAsList[i]);
                                    PO.Fee -= fee;
                                    PO.FinalRate = PO.Fee + .14m;
                                    break;
                                case 8:
                                    PO.ID = csvRowAsList[i];
                                    break;
                            }
                        }
                        
                        if (PO.Price > 0)
                        {
                            if (PO.Fee == 0) { PO.IsAlcoholic = true; }
                            PurchaseOrders.Add(PO);
                        }
                    }
                }

                //Getting Unique Dates and Unique Store Numbers
                List<int> uniqueDates = new List<int>();
                List<int> uniqueStores = new List<int>();

                foreach (PurchaseOrder Order in PurchaseOrders)
                {
                    if (!uniqueDates.Contains(Int32.Parse(Order.Date)))
                        uniqueDates.Add(Int32.Parse(Order.Date));
                    
                    if (!uniqueStores.Contains(Order.StoreNum))
                        uniqueStores.Add(Order.StoreNum);
                }

		        //Order the dates from most recent to oldest
		        uniqueDates.Sort();
		        uniqueDates.Reverse();

                //Final String for putting into new file:
                string finalString = "";

                //Match Unique Dates to PO for grouping
                foreach (int date in uniqueDates)
                {
                    //Date as string
                    string strDate = date.ToString();

                    //Logic for this date where a given store has POs
                    List<Store> storesWithAlcSales = new List<Store>();
                    List<Store> storesWithAnySales = new List<Store>();
                    foreach (int store in uniqueStores)
                    {
                        //Stores that have any orders on this date
                        List<PurchaseOrder> allStoreOrdersForDate = new List<PurchaseOrder>();
                        //Stores that have alcoholic orders on this date
                        List<PurchaseOrder> allAlcStoreOrdersForDate = new List<PurchaseOrder>();
                        foreach (PurchaseOrder Order in PurchaseOrders)
                        {
                            if (Order.StoreNum == store && Order.Date == strDate)
                            {
                                allStoreOrdersForDate.Add(Order);
                                if (Order.IsAlcoholic) { allAlcStoreOrdersForDate.Add(Order); }
                            }
                        }
                        if (allStoreOrdersForDate.Count() > 0)
                        {
                            Store StoreWithSales = new Store(store, allStoreOrdersForDate, allAlcStoreOrdersForDate);
                            storesWithAnySales.Add(StoreWithSales);
                            if (allAlcStoreOrdersForDate.Count() > 0) { storesWithAlcSales.Add(StoreWithSales); }
                        }
                    }

                    //START OF GATHERING FINAL STRING TO PUT IN NEW FILE
                    if (storesWithAnySales.Count() > 0) //Are there stores with sales
                    {
                        if (storesWithAlcSales.Count() > 0) //Are there stores with alcohol sales
                        {
                            finalString += SectionHeader(strDate, true);
                            decimal dateAlcTotal = 0;

                            foreach (Store store in storesWithAlcSales)
                            {
                                decimal alcSaleTotal = 0;

                                foreach (PurchaseOrder Order in store.AlcPOs)
                                {
                                    alcSaleTotal += Order.Price;
                                    dateAlcTotal -= Order.Price;

                                    var formatAlcSaleTotal = String.Format("FA:{0, 5} {1, 4} {2, 4} {3, 4} {4, 15}\r\n", 1, store.StoreNum, 99, 2153, Order.Price);
                                    finalString += formatAlcSaleTotal;
                                }
                            }

                            var formatDateAlcTotal = String.Format("FA:{0, 5} {1, 4} {2, 4} {3, 4} {4, 15}\r\nEND:\r\n", 1, 90, 99, 2146, dateAlcTotal);
                            finalString += formatDateAlcTotal;
                        }

                        finalString += SectionHeader(strDate, false);

                        var allStoreSums = 0m;

                        foreach (Store StoreInst in storesWithAnySales)
                        {
                            var sumOfOrderFR = 0m;

                            foreach (PurchaseOrder Order in StoreInst.POs) { sumOfOrderFR += Order.FinalRate; }

                            var formatStoreSum = String.Format("FA:{0, 5} {1, 4} {2, 4} {3, 4} {4, 15}\r\n", 1, StoreInst.StoreNum, 99, 4520, sumOfOrderFR);
                            finalString += formatStoreSum;
                            allStoreSums -= sumOfOrderFR;
                        }

                        var formatAllStoreSum = String.Format("FA:{0, 5} {1, 4} {2, 4} {3, 4} {4, 15}\r\nEND:\r\n", 1, 90, 99, 2146, allStoreSums);
                        finalString += formatAllStoreSum;
                    }
                    //END OF FINAL STRING COMBINATION
                }

                //Create new prn file from converted string
                //Formatted name
                string newFilePath = $"{fileRoot}\\gltrnimprt.prn";

                //If file exists, delete and replace (for new creation date)
                try
                {
                    if (File.Exists(newFilePath)) { File.Delete(newFilePath); }
					
                    //Create the file and use the finalString for the text
                    using (StreamWriter sw = File.CreateText(newFilePath))
                    {
                        sw.WriteLine($"{finalString}");
                    }
                }
		        catch (IOException e)
                {
					var root = AppDomain.CurrentDomain.BaseDirectory;
					var errorTextFile = $"{root}\\ErrorsLog.txt";
					var uniqueErrorFile = GetUniqueFilename(errorTextFile);

					var logText = $"The following error occurred:\n\n{e}";
					Console.WriteLine($"\n\nThe following error occurred:\n\n{e}");

					using (StreamWriter sw = File.CreateText(uniqueErrorFile))
					{
						sw.WriteLine($"{logText}");
					}
				}
			}
            catch (Exception ex)
            {
				fileRoot = AppDomain.CurrentDomain.BaseDirectory;
				var errorTextFile = $"{fileRoot}\\ErrorsLog.txt";
				var uniqueErrorFile = GetUniqueFilename(errorTextFile);

				var logText = $"The following error occurred:\n\n{ex}";
				Console.WriteLine($"\n\nThe following error occurred:\n\n{ex}");

				using (StreamWriter sw = File.CreateText(uniqueErrorFile))
				{
					sw.WriteLine($"{logText}");
				}
			}
		}

		//Define other methods/functions here
		static string SectionHeader(string date, bool alcohol)
		{
			CultureInfo provider = CultureInfo.InvariantCulture;
			string formatDate = DateTime.ParseExact(date, "yyMMdd", provider).ToString("MM/dd/yy");
			string header = $"0001\r\n0001\r\n0090\r\nJE\r\n{formatDate}\r\n";
            header += (alcohol == true)
                ? $"APPLY BEER & WINE P.O.\r\nN\r\n  0\r\n"
                : $"APPLY MONEY ORDER COMMISION\r\nN\r\n  0\r\n";
			return header;
		}

		static string GetUniqueFilename(string fullPath)
		{
			if (!Path.IsPathRooted(fullPath)) { fullPath = Path.GetFullPath(fullPath); }
			if (File.Exists(fullPath))
			{
				String filename = Path.GetFileName(fullPath);
				String path = fullPath.Substring(0, fullPath.Length - filename.Length);
				String filenameWOExt = Path.GetFileNameWithoutExtension(fullPath);
				String ext = Path.GetExtension(fullPath);
				int n = 1;
				do { fullPath = Path.Combine(path, String.Format("{0} ({1}){2}", filenameWOExt, (n++), ext)); }
				while (File.Exists(fullPath));
			}
			return fullPath;
		}

		//Define other classes here
		class PurchaseOrder
		{
			public string ID { get; set; }
			public string Date { get; set; }
			public int StoreNum { get; set; }
			public decimal Price { get; set; }
			public decimal Fee { get; set; }
			public decimal FinalRate { get; set; }
			public bool IsAlcoholic { get; set; }

			public PurchaseOrder()
			{
				this.ID = "0";
				this.Date = "000000";
				this.StoreNum = 0;
				this.Price = 0m;
				this.Fee = 0m;
				this.FinalRate = 0m;
				this.IsAlcoholic = false;
			}

			public PurchaseOrder(string id, string date, int storeNum, decimal price, decimal fee)
			{
				this.ID = id;
				this.Date = date;
				this.StoreNum = storeNum;
				this.Price = price;
				this.Fee = fee;
				this.FinalRate = 0m;
				this.IsAlcoholic = (this.Price > 0 && fee == 0);
			}
		}

		class Store
		{
			public int StoreNum;
			public List<PurchaseOrder> POs = new List<PurchaseOrder>();
			public List<PurchaseOrder> AlcPOs = new List<PurchaseOrder>();

			public Store(int storeNum, List<PurchaseOrder> purchaseorders, List<PurchaseOrder> alcpurchaseorders)
			{
				this.StoreNum = storeNum;
				this.POs.AddRange(purchaseorders);
				this.AlcPOs.AddRange(alcpurchaseorders);
			}
		}
	}
}
