﻿using System;
using System.IO;
using System.Web.Mvc;
using System.Xml.Linq;
using Orchard.ContentManagement;
using Orchard.FileSystems.AppData;
using Orchard.ImportExport.Models;
using Orchard.ImportExport.Services;
using Orchard.Recipes.Models;
using Orchard.Services;

namespace Orchard.ImportExport.DeploymentTargets {
    public class OrchardDeploymentTarget : IDeploymentTarget, IDeploymentTargetProvider {
        private readonly IImportExportService _importExportService;
        private readonly ISigningService _signingService;
        private RemoteOrchardDeploymentPart DeploymentPart { get; set; }
        private Lazy<RemoteOrchardApiClient> Client { get; set; }
        private readonly IClock _clock;
        private readonly UrlHelper _url;
        private readonly IDeploymentPackageBuilder _deploymentPackageBuilder;
        private readonly IAppDataFolder _appData;

        public OrchardDeploymentTarget(
            IImportExportService importExportService,
            ISigningService signingService,
            IClock clock,
            UrlHelper url,
            IDeploymentPackageBuilder deploymentPackageBuilder,
            IAppDataFolder appData
            ) {
            _importExportService = importExportService;
            _signingService = signingService;
            _clock = clock;
            _url = url;
            _deploymentPackageBuilder = deploymentPackageBuilder;
            _appData = appData;
        }

        public DeploymentTargetMatch Match(IContent targetConfiguration) {
            if (targetConfiguration.Is<RemoteOrchardDeploymentPart>()) {
                DeploymentPart = targetConfiguration.As<RemoteOrchardDeploymentPart>();
                Client = new Lazy<RemoteOrchardApiClient>(() => new RemoteOrchardApiClient(DeploymentPart, _signingService, _clock, _appData));
                return new DeploymentTargetMatch { DeploymentTarget = this, Priority = 0 };
            }
            return null;
        }

        public void PushDeploymentFile(string executionId, string deploymentFilePath) {
            if (Path.GetExtension(deploymentFilePath) == ".xml") {
                var actionUrl = _url.Action("Recipe", "Import", new {
                    area = "Orchard.ImportExport",
                    executionId
                });
                var recipe = File.ReadAllText(deploymentFilePath);
                Client.Value.Post(actionUrl, recipe, "text/xml");
            }
            else {
                var actionUrl = _url.Action("DeployContent", "Import", new {
                    area = "Orchard.ImportExport",
                    executionId
                });
                using (var deploymentStream = File.OpenRead(deploymentFilePath)) {
                    Client.Value.PostStream(actionUrl, deploymentStream);
                }
            }
        }

        public void PushRecipe(string executionId, string recipeText) {
            var actionUrl = _url.Action("Recipe", "Import", new {
                area = "Orchard.ImportExport",
                executionId
            });
            Client.Value.Post(actionUrl, recipeText, "text/xml");
        }

        public bool? GetRecipeDeploymentStatus(string executionId) {
            var actionUrl = _url.Action("RecipeJournal", "Import", new {
                area = "Orchard.ImportExport",
                executionId
            });
            var journal = Client.Value.Get(actionUrl);
            var element = XElement.Parse(journal);
            var statusElement = element.Element("Status");
            bool status;
            if (statusElement != null && Boolean.TryParse(statusElement.Value, out status)) {
                return status;
            }
            return null;
        }

        public void PushContent(IContent content, bool deployAsDraft = false) {
            var actionUrl = _url.Action("DeployContent", "Import", new {
                area = "Orchard.ImportExport"
            });
            var exportedFilePathResult = _importExportService.Export(
                new[] { content.ContentItem.ContentType },
                new[] { content.ContentItem },
                new ExportOptions {
                    ExportData = true,
                    ExportAsDraft = deployAsDraft,
                    VersionHistoryOptions = VersionHistoryOptions.Published
                });
            var exportFilePath = exportedFilePathResult.FileName;
            if (Path.GetExtension(exportedFilePathResult.FileName) == ".nupkg") {
                using (var packageStream = File.OpenRead(exportFilePath)) {
                    Client.Value.PostStream(actionUrl, packageStream);
                }
            }
            else {
                var recipeText = File.ReadAllText(exportFilePath);
                Client.Value.Post(actionUrl, recipeText, "text/xml");
            }
            File.Delete(exportFilePath);
        }
    }
}
