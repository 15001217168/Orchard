﻿using System.Collections.Generic;

namespace Orchard.ImportExport.ViewModels {
    public class ExportViewModel {
        public IList<ContentTypeEntry> ContentTypes { get; set; }
        public IList<CustomStepEntry> CustomSteps { get; set; }
        public bool Files { get; set; }
        public bool ExportAsDraft { get; set; }
        public bool Metadata { get; set; }
        public bool Data { get; set; }
        public int? ImportBatchSize { get; set; }
        public string DataImportChoice { get; set; }
        public bool SiteSettings { get; set; }
        public bool SetupRecipe { get; set; }
        public string RecipeName { get; set; }
        public string RecipeDescription { get; set; }
        public string RecipeWebsite { get; set; }
        public string RecipeTags { get; set; }
        public string RecipeVersion { get; set; }
    }

    public class ContentTypeEntry {
        public string ContentTypeName { get; set; }
        public bool IsChecked { get; set; }
    }

    public class CustomStepEntry {
        public string CustomStep { get; set; }
        public bool IsChecked { get; set; }
    }
}
