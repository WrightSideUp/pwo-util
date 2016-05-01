using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.DataMovement;

namespace BulkImageUploader
{
    class Program
    {
        private static void Main(string[] args)
        {
            ProcessImages();
        }

        private static void ProcessImages()
        {
            var dt = new DataTable();
            CloudBlobClient client = Util.GetCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("asset-images");

            using (
                var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["AssetManagement"].ConnectionString)
                )
            {
                conn.Open();

                using (SqlCommand cmd = conn.CreateCommand(),
                    renameCommand = conn.CreateCommand(),
                    deleteCommand = conn.CreateCommand())
                {
                    var sql =
                        "SELECT m.AssetId, m.MediaId, m.Sequence FROM Media m INNER JOIN Asset a ON m.AssetId = a.AssetId WHERE a.CompanyId = 2 AND m.MediaTypeId = 1 ORDER BY m.AssetId, m.Sequence ";
                    cmd.CommandText = sql;

                    renameCommand.CommandText = "UPDATE Media SET FileName = @name WHERE MediaId = @id";
                    renameCommand.Parameters.Add("@name", SqlDbType.VarChar);
                    renameCommand.Parameters.Add("@id", SqlDbType.Int);

                    deleteCommand.CommandText = "DELETE FROM Media WHERE MediaId = @id";
                    deleteCommand.Parameters.Add("@id", SqlDbType.Int);

                    using (var r = cmd.ExecuteReader())
                    {
                        dt.Load(r);
                    }

                    var assetFiles = from row in dt.AsEnumerable()
                        group new {Sequence = row.Field<int>("Sequence"), MediaId = row.Field<int>("MediaId") } by
                            row.Field<int>("AssetId")
                        into g
                        select g;

                    foreach (var asset in assetFiles)
                    {
                        foreach (var file in asset)
                        {
                            ProcessFile(asset.Key, file.Sequence, file.MediaId, container, renameCommand, deleteCommand);
                        }
                    }
                }

                conn.Close();
            }

        }

        private static void ProcessFile(int assetId, int sequence, int mediaId, CloudBlobContainer container, SqlCommand renameCommand, SqlCommand deleteCommand)
        {
            var blobName = $"{assetId}\\{sequence}.jpg";
            var blob = container.GetBlockBlobReference(blobName);

            if (blob.Exists())
            {
                var newName = $"{assetId}\\{mediaId}.jpg";
                container.RenameBlob(blobName, newName);
                renameCommand.Parameters["@name"].Value = newName;
                renameCommand.Parameters["@id"].Value = mediaId;
                renameCommand.ExecuteNonQuery();
                Console.WriteLine($"Renamed {blobName} to {newName}");
            }
            else
            {
                deleteCommand.Parameters["@id"].Value = mediaId;
                deleteCommand.ExecuteNonQuery();
                Console.WriteLine($"Deleted MediaId {mediaId}");
            }
        }


        // This method originally used to upload blobs
        private static async Task ProcessFileOriginal(int assetId, int sequence, string fileName)
        {
            var url = "http://www.equipment.solutions4mfg.com/resource/photos/" + fileName + ".jpg";
            var client = new HttpClient();
            byte[] response = new byte[0];

            try
            {
                response = await client.GetByteArrayAsync(url);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error downloading file {url}.");
            }

            if (response.Length > 0)
            {
                var blobName = $"{assetId}\\{sequence}.jpg";

                CloudBlob blob = Util.GetCloudBlob("asset-images", blobName, BlobType.BlockBlob);
                var options = new UploadOptions {ContentType = "image/jpg"};

                try
                {
                    await TransferManager.UploadAsync(new MemoryStream(response), blob);
                    Console.WriteLine($"File {fileName} is uploaded to {blob.Uri} successfully.");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error uploading file: " + blobName + ".  " + e.Message);
                }
            }
        }
    }
}
