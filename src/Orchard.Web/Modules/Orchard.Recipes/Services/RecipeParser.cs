﻿using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Orchard.Logging;
using Orchard.Recipes.Models;

namespace Orchard.Recipes.Services {
    public class RecipeParser : Component, IRecipeParser {
        private const int BatchCountLimit = 20;

        public Recipe ParseRecipe(string recipeText) {
            var recipe = new Recipe();
            
            if (string.IsNullOrEmpty(recipeText)) {
                throw new Exception("Recipe is empty");
            }

            var recipeTree = XElement.Parse(recipeText, LoadOptions.PreserveWhitespace);
            var recipeSteps = new List<RecipeStep>();

            foreach (var element in recipeTree.Elements()) {
                // Recipe metadata.
                if (element.Name.LocalName == "Recipe") {
                    foreach (var metadataElement in element.Elements()) {
                        switch (metadataElement.Name.LocalName) {
                            case "Name":
                                recipe.Name = metadataElement.Value;
                                break;
                            case "Description":
                                recipe.Description = metadataElement.Value;
                                break;
                            case "Author":
                                recipe.Author = metadataElement.Value;
                                break;
                            case "WebSite":
                                recipe.WebSite = metadataElement.Value;
                                break;
                            case "Version":
                                recipe.Version = metadataElement.Value;
                                break;
                            case "IsSetupRecipe":
                                recipe.IsSetupRecipe = !string.IsNullOrEmpty(metadataElement.Value) ? bool.Parse(metadataElement.Value) : false;
                                break;
                            case "ExportUtc":
                                recipe.ExportUtc = !string.IsNullOrEmpty(metadataElement.Value) ? (DateTime?)XmlConvert.ToDateTime(metadataElement.Value, XmlDateTimeSerializationMode.Utc) : null;
                                break;
                            case "Category":
                                recipe.Category = metadataElement.Value;
                                break;
                            case "Tags":
                                recipe.Tags = metadataElement.Value;
                                break;
                            default:
                                Logger.Warning("Unrecognized recipe metadata element '{0}' encountered; skipping.", metadataElement.Name.LocalName);
                                break;
                        }
                    }
                }
                // Recipe step.
                else {
                    recipeSteps.Add(new RecipeStep { Name = element.Name.LocalName, Step = element });
                }
            }

            recipe.RecipeSteps = recipeSteps;

            return recipe;
        }
    }
}