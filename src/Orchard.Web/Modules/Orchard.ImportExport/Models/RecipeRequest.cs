﻿using System;
using System.Collections.Generic;
using Orchard.Recipes.Models;

namespace Orchard.ImportExport.Models {
    public class RecipeRequest {
        public bool IncludeMetadata { get; set; }
        public bool IncludeData { get; set; }
        public bool IncludeFiles { get; set; }
        public bool DeployAsDrafts { get; set; }
        public string QueryIdentity { get; set; }
        public VersionHistoryOptions VersionHistoryOption { get; set; }
        public DateTime? DeployChangesAfterUtc { get; set; }
        public List<string> ContentIdentities { get; set; }
        public List<string> ContentTypes { get; set; }
        public List<DeploymentMetadata> DeploymentMetadata { get; set; }
    }
}