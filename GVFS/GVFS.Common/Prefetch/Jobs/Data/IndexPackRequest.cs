﻿namespace GVFS.Common.Prefetch.Jobs.Data
{
    public class IndexPackRequest
    {
        public IndexPackRequest(string tempPackFile, BlobDownloadRequest downloadRequest)
        {
            this.TempPackFile = tempPackFile;
            this.DownloadRequest = downloadRequest;
        }

        public BlobDownloadRequest DownloadRequest { get; }

        public string TempPackFile { get; }
    }
}