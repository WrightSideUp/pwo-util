using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

public static class BlobContainerExtensions
{
    public static void RenameBlob(this CloudBlobContainer container, string oldName, string newName)
    {
        RenameBlobAsync(container, oldName, newName).Wait();
    }

    public static async Task RenameBlobAsync(this CloudBlobContainer container, string oldName, string newName)
    {
        var source = await container.GetBlobReferenceFromServerAsync(oldName);
        var target = container.GetBlockBlobReference(newName);

        await target.StartCopyFromBlobAsync(source.Uri);

        while (target.CopyState.Status == CopyStatus.Pending)
            await Task.Delay(100);

        if (target.CopyState.Status != CopyStatus.Success)
            throw new ApplicationException("Rename failed: " + target.CopyState.Status);

        await source.DeleteAsync();
    }
}