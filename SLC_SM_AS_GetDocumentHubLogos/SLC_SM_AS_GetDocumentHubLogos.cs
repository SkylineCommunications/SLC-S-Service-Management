namespace SLCSMASGetDocumentHubLogos
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Solutions.DocumentHub.API.DocHubClient;
	using Skyline.DataMiner.Solutions.DocumentHub.API.FileAdapters;
	using Skyline.DataMiner.Solutions.DocumentHub.Automation;
	using Skyline.DataMiner.Solutions.DocumentHub.SDM.Exposers;
	using Skyline.DataMiner.Solutions.DocumentHub.SDM.Helpers;
	using Skyline.DataMiner.Solutions.DocumentHub.SDM.Models;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	/// <summary>
	/// Lists selectable logo files from Document Hub.
	/// </summary>
	public class Script
	{
		private const int MaxFiles = 500;

		/// <summary>
		/// Executes the script.
		/// </summary>
		/// <param name="engine">Link with the SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				throw;
			}
			catch (ScriptForceAbortException)
			{
				throw;
			}
			catch (ScriptTimeoutException)
			{
				throw;
			}
			catch (Exception ex)
			{
				engine.Log("Run|Unexpected exception: " + ex.ToString());
				engine.ExitFail("Runtime operation failed.");
			}
		}

		private static object ToResponse(string bucketName, IDocHubFile file)
		{
			string filePath = file.GetFilePath() ?? String.Empty;
			string webPath = file.GetWebPath() ?? String.Empty;
			return new
			{
				bucket = bucketName ?? String.Empty,
				fileName = file.GetFile() ?? String.Empty,
				icon = NormalizeWebPath(webPath, filePath),
				extension = file.GetExtension() ?? String.Empty,
				directory = GetDirectory(filePath, webPath),
			};
		}

		private static string NormalizeWebPath(string webPath, string filePath)
		{
			if (Uri.TryCreate(webPath, UriKind.Absolute, out Uri uri)
				&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
			{
				return uri.AbsolutePath ?? String.Empty;
			}

			if (!String.IsNullOrWhiteSpace(webPath))
			{
				return webPath.StartsWith("/", StringComparison.Ordinal) ? webPath : $"/{webPath}";
			}

			if (String.IsNullOrWhiteSpace(filePath))
			{
				return String.Empty;
			}

			string normalizedFilePath = filePath.Replace('\\', '/');
			const string publicMarker = "/Public/";
			int publicIndex = normalizedFilePath.IndexOf(publicMarker, StringComparison.OrdinalIgnoreCase);
			return publicIndex >= 0 ? normalizedFilePath.Substring(publicIndex) : normalizedFilePath;
		}

		private static string GetDirectory(string filePath, string webPath)
		{
			string path = NormalizeWebPath(webPath, filePath);
			return String.IsNullOrWhiteSpace(path)
				? String.Empty
				: Path.GetDirectoryName(path)?.Replace('\\', '/') ?? String.Empty;
		}

		private static DocumentBucket GetBucket(IDocumentHubApiHelper helper, string bucketName)
		{
			if (!String.IsNullOrWhiteSpace(bucketName))
			{
				return helper.DocumentBuckets
					.Read(DocumentBucketExposers.Name.Equal(bucketName.Trim()))
					.FirstOrDefault();
			}

			return helper.DocumentBuckets
				.Read(new TRUEFilterElement<DocumentBucket>())
				.OrderByDescending(bucket => bucket.IsDefault)
				.ThenBy(bucket => bucket.Name ?? String.Empty, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault();
		}

		private static bool MatchesFilter(IDocHubFile file, string nameFilter)
		{
			return String.IsNullOrWhiteSpace(nameFilter)
				|| (file.GetFile() ?? String.Empty).IndexOf(nameFilter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static List<object> ReadFiles(DocHubClient client, DocumentBucket bucket, string nameFilter)
		{
			return client.Files.ReadFiles(bucket)
				.Where(file => MatchesFilter(file, nameFilter))
				.OrderBy(file => file.GetFile(), StringComparer.OrdinalIgnoreCase)
				.Take(MaxFiles)
				.Select(file => ToResponse(bucket.Name, file))
				.ToList();
		}

		private static void RunSafe(IEngine engine)
		{
			string bucketName = engine.ReadScriptParamFromApp("Bucket Name") ?? String.Empty;
			string nameFilter = engine.ReadScriptParamFromApp("Name Filter") ?? String.Empty;
			IDocumentHubApiHelper helper = engine.GetDocumentHubApiHelper();
			DocumentBucket bucket = GetBucket(helper, bucketName);
			DocHubClient client = engine.GetDocHubClient();
			var files = bucket == null ? new List<object>() : ReadFiles(client, bucket, nameFilter);
			engine.AddScriptOutput("Response", JsonConvert.SerializeObject(files));
		}
	}
}
