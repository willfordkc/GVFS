﻿namespace GVFS.Common.Prefetch.Jobs.Data
{
    public class SearchTreeRequest
    {
        public SearchTreeRequest(string treeSha, string rootPath, bool shouldRecurse)
        {
            this.TreeSha = treeSha;
            this.RootPath = rootPath;
            this.ShouldRecurse = shouldRecurse;
        }

        public bool ShouldRecurse { get; }

        public string TreeSha { get; }

        public string RootPath { get; }
    }
}
