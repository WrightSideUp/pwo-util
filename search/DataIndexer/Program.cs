﻿using System;
using System.Configuration;
using System.Threading;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace DataIndexer
{
    class Program
    {
        private const string AssetIndex = "assets";
        private const string AssetDataSource = "asset-datasource";
        private const string AssetIndexer = "asset-indexer";

        private static SearchServiceClient _searchClient;
        private static SearchIndexClient _indexClient;

        // This Sample shows how to delete, create, upload documents and query an index
        static void Main(string[] args)
        {
            string searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
            string apiKey = ConfigurationManager.AppSettings["SearchServiceApiKey"];

            // Create an HTTP reference to the catalog index
            _searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));
            _indexClient = _searchClient.Indexes.GetClient(AssetIndex);

            Console.WriteLine("{0}", "Deleting index, data source, and indexer...\n");
            if (DeleteIndexingResources())
            {
                Console.WriteLine("{0}", "Creating index...\n");
                CreateIndex();
                Console.WriteLine("{0}", "Sync documents from Azure SQL...\n");
                SyncDataFromAzureSQL();
            }
            Console.WriteLine("{0}", "Complete.  Press any key to end application...\n");
            Console.ReadKey();
        }

        private static bool DeleteIndexingResources()
        {
            // Delete the index, data source, and indexer.
            try
            {
                _searchClient.Indexes.Delete(AssetIndex);
                _searchClient.DataSources.Delete(AssetDataSource);
                _searchClient.Indexers.Delete(AssetIndexer);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error deleting indexing resources: {0}\r\n", ex.Message);
                Console.WriteLine("Did you remember to add your SearchServiceName and SearchServiceApiKey to the app.config?\r\n");
                return false;
            }

            return true;
        }

        private static void CreateIndex()
        {
            // Create the Azure Search index based on the included schema
            try
            {
                var definition = new Index()
                {
                    Name = AssetIndex,
                    Fields = new[]
                    {
                        new Field("AssetId",         DataType.String)         { IsKey = true,  IsSearchable = false,  IsFilterable = false,  IsSortable = false,  IsFacetable = false,  IsRetrievable = true},
                        new Field("AssetIdentifier", DataType.String)         { IsKey = false, IsSearchable = true,   IsFilterable = true,   IsSortable = true,   IsFacetable = false,  IsRetrievable = true},
                        new Field("Name",            DataType.String)         { IsKey = false, IsSearchable = true,   IsFilterable = true,   IsSortable = true,   IsFacetable = false,  IsRetrievable = true},
                        new Field("AssetCategoryId", DataType.Int32)          { IsKey = false, IsSearchable = false,  IsFilterable = true,   IsSortable = false,  IsFacetable = false,  IsRetrievable = true},
                        new Field("Category",        DataType.String)         { IsKey = false, IsSearchable = true,   IsFilterable = true,   IsSortable = true,   IsFacetable = false,  IsRetrievable = true},
                        new Field("Manufacturer",    DataType.String)         { IsKey = false, IsSearchable = true,   IsFilterable = true,   IsSortable = true,   IsFacetable = false,  IsRetrievable = true},
                        new Field("Description",     DataType.String)         { IsKey = false, IsSearchable = true,   IsFilterable = true,   IsSortable = true,   IsFacetable = false,  IsRetrievable = true},
                        new Field("Condition",       DataType.String)         { IsKey = false, IsSearchable = false,  IsFilterable = true,   IsSortable = true,   IsFacetable = false,  IsRetrievable = true},
                        new Field("IsFeatured",      DataType.String)         { IsKey = false, IsSearchable = true,   IsFilterable = true,   IsSortable = true,   IsFacetable = false,  IsRetrievable = true}, 
                        new Field("UpdatedDate",     DataType.DateTimeOffset) { IsKey = false, IsSearchable = false,  IsFilterable = true,   IsSortable = false,  IsFacetable = false,  IsRetrievable = true}
                    },
                    Suggesters = new[]
                    {
                        new Suggester("assetsSuggester", SuggesterSearchMode.AnalyzingInfixMatching, new[] {"Name", "Category", "AssetIdentifier", "Manufacturer"}), 
                    }
                };

                _searchClient.Indexes.Create(definition);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating index: {0}\r\n", ex.Message);
            }

        }

        private static void SyncDataFromAzureSQL()
        {
            // This will use the Azure Search Indexer to synchronize data from Azure SQL to Azure Search
            Console.WriteLine("{0}", "Creating Data Source...\n");
            var dataSource =
                new DataSource()
                {
                    Name = AssetDataSource,
                    Description = "AssetManagement DataSet",
                    Type = DataSourceType.AzureSql,
                    Credentials = new DataSourceCredentials("Server=fckpvybjkl.database.windows.net,1433;Database=AssetManagement;User ID=brucewright18;Password=Mallard18;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;"),
                    Container = new DataContainer("vAssetPwoCustomerSearch")
                };

            try
            {
                _searchClient.DataSources.Create(dataSource);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating data source: {0}", ex.Message);
                return;
            }

            Console.WriteLine("{0}", "Creating Indexer and syncing data...\n");

            var indexer =
                new Indexer()
                {
                    Name = AssetIndexer,
                    Description = "Asset Management data indexer",
                    DataSourceName = dataSource.Name,
                    TargetIndexName = AssetIndex
                };

            try
            {
                _searchClient.Indexers.Create(indexer);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating and running indexer: {0}", ex.Message);
                return;
            }

            bool running = true;
            Console.WriteLine("{0}", "Synchronization running...\n");
            while (running)
            {
                IndexerExecutionInfo status = null;

                try
                {
                    status = _searchClient.Indexers.GetStatus(indexer.Name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error polling for indexer status: {0}", ex.Message);
                    return;
                }

                IndexerExecutionResult lastResult = status.LastResult;
                if (lastResult != null)
                {
                    switch (lastResult.Status)
                    {
                        case IndexerExecutionStatus.InProgress:
                            Console.WriteLine("{0}", "Synchronization running...\n");
                            Thread.Sleep(1000);
                            break;

                        case IndexerExecutionStatus.Success:
                            running = false;
                            Console.WriteLine("Synchronized {0} rows...\n", lastResult.ItemCount);
                            break;

                        default:
                            running = false;
                            Console.WriteLine("Synchronization failed: {0}\n", lastResult.ErrorMessage);
                            break;
                    }
                }
            }
        }
    }
}