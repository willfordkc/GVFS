﻿using GVFS.Common;
using GVFS.Common.NamedPipes;
using GVFS.Common.Tracing;
using GVFS.Windows;
using System.Collections.Generic;
using System.Linq;

namespace GVFS.Service.Handlers
{
    public class GetActiveRepoListHandler : MessageHandler
    {
        private NamedPipeServer.Connection connection;
        private NamedPipeMessages.GetActiveRepoListRequest request;
        private ITracer tracer;
        private RepoRegistry registry;

        public GetActiveRepoListHandler(
            ITracer tracer,
            RepoRegistry registry,
            NamedPipeServer.Connection connection,
            NamedPipeMessages.GetActiveRepoListRequest request)
        {
            this.tracer = tracer;
            this.registry = registry;
            this.connection = connection;
            this.request = request;
        }

        public void Run()
        {
            string errorMessage;
            NamedPipeMessages.GetActiveRepoListRequest.Response response = new NamedPipeMessages.GetActiveRepoListRequest.Response();
            response.State = NamedPipeMessages.CompletionState.Success;
            response.RepoList = new List<string>();

            List<RepoRegistration> repos;
            if (this.registry.TryGetActiveRepos(out repos, out errorMessage))
            {
                List<string> tempRepoList = repos.Select(repo => repo.EnlistmentRoot).ToList();

                foreach (string repoRoot in tempRepoList)
                {
                    if (!this.IsValidRepo(repoRoot))
                    {
                        if (!this.registry.TryRemoveRepo(repoRoot, out errorMessage))
                        {
                            this.tracer.RelatedInfo("Removing an invalid repo failed with error: " + response.ErrorMessage);
                        }
                        else
                        {
                            this.tracer.RelatedInfo("Removed invalid repo entry from registry: " + repoRoot);
                        }
                    }
                    else
                    {
                        response.RepoList.Add(repoRoot);
                    }
                }
            }
            else
            {
                response.ErrorMessage = errorMessage;
                response.State = NamedPipeMessages.CompletionState.Failure;
                this.tracer.RelatedError("Get active repo list failed with error: " + response.ErrorMessage);
            }

            this.WriteToClient(response.ToMessage(), this.connection, this.tracer);
        }

        private bool IsValidRepo(string repoRoot)
        {
            WindowsGitInstallation windowsGitInstallation = new WindowsGitInstallation();
            string gitBinPath = windowsGitInstallation.GetInstalledGitBinPath();
            string hooksPath = ProcessHelper.WhereDirectory(GVFSConstants.GVFSHooksExecutableName);
            GVFSEnlistment enlistment = null;

            try
            {
                enlistment = GVFSEnlistment.CreateFromDirectory(repoRoot, gitBinPath, hooksPath);
            }
            catch (InvalidRepoException)
            {
                return false;
            }

            if (enlistment == null)
            {
                return false;
            }

            return true;
        }
    }
}
