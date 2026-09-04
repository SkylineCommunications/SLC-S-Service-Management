/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

31/08/2026  1.0.0.1        SKA           Initial version
****************************************************************************
*/
namespace SLC_SM_Import_Service_Categories
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using ServiceModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	public class Script
	{
		private const string DefaultJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\categories.json";

		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
			}
			catch (ScriptForceAbortException)
			{
			}
			catch (ScriptTimeoutException)
			{
			}
			catch (InteractiveUserDetachedException)
			{
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
			}
		}

		private static void RunSafe(IEngine engine)
		{
			string jsonPath = engine.ReadScriptParamFromApp("JSON File Path");
			if (String.IsNullOrWhiteSpace(jsonPath))
			{
				jsonPath = DefaultJsonPath;
			}

			if (!File.Exists(jsonPath))
			{
				throw new FileNotFoundException($"JSON import file was not found: '{jsonPath}'");
			}

			var payload = JsonConvert.DeserializeObject<CategoriesRoot>(File.ReadAllText(jsonPath));
			if (payload?.Categories == null || payload.Categories.Count == 0)
			{
				throw new InvalidOperationException($"No categories were found in '{jsonPath}'.");
			}

			var helper = new DataHelperServiceCategory(engine.GetUserConnection());
			var existing = helper.Read();

			int created = 0;
			int updated = 0;
			int index = 0;
			var failures = new List<string>();

			foreach (var source in payload.Categories)
			{
				index++;

				if (source == null || String.IsNullOrWhiteSpace(source.CategoryName))
				{
					failures.Add($"Category #{index} was skipped: a non-empty 'categoryName' is required.");
					continue;
				}

				try
				{
					var match = existing.FirstOrDefault(
						x => String.Equals(x.Name, source.CategoryName, StringComparison.InvariantCultureIgnoreCase)
						     && String.Equals(x.Type ?? String.Empty, source.CategoryType ?? String.Empty, StringComparison.InvariantCultureIgnoreCase));

					var category = match ?? new ServiceModels.ServiceCategory
					{
						ID = Guid.NewGuid(),
					};

					category.Name = source.CategoryName;
					category.Type = source.CategoryType ?? String.Empty;
					category.Icon = source.Icon ?? String.Empty;

					helper.CreateOrUpdate(category);

					if (match == null)
					{
						created++;
						existing.Add(category);
					}
					else
					{
						updated++;
					}
				}
				catch (Exception e)
				{
					failures.Add($"Category '{source.CategoryName}' was not imported: {e.Message}");
				}
			}

			engine.GenerateInformation($"[Import Service Categories] Completed. Created: {created}, Updated: {updated}. Failed: {failures.Count}. Source file: {jsonPath}");

			if (failures.Count > 0)
			{
				engine.ExitFail($"[Import Service Categories] {failures.Count} record(s) failed and were skipped: {String.Join(" | ", failures)}");
			}
		}

		private sealed class CategoriesRoot
		{
			[JsonProperty("categories")]
			public List<CategoryImport> Categories { get; set; }
		}

		private sealed class CategoryImport
		{
			[JsonProperty("categoryType")]
			public string CategoryType { get; set; }

			[JsonProperty("categoryName")]
			public string CategoryName { get; set; }

			[JsonProperty("icon")]
			public string Icon { get; set; }
		}
	}
}
