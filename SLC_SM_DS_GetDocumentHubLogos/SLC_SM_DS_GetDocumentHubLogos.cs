/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

20/08/2026	1.0.0.1		SKA			Initial version
****************************************************************************
*/

namespace SLCSMDSGetDocumentHubLogos
{
	using System;
	using System.IO;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Solutions.DocumentHub.API.FileAdapters;
	using Skyline.DataMiner.Solutions.DocumentHub.GQI;
	using Skyline.DataMiner.Solutions.DocumentHub.SDM.Exposers;
	using Skyline.DataMiner.Solutions.DocumentHub.SDM.Helpers;
	using Skyline.DataMiner.Solutions.DocumentHub.SDM.Models;
	using SLC_SM_Common.Extensions;

	/// <summary>
	/// Returns logo files from a specific Document Hub bucket.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetDocumentHubLogos : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "SLC_SM_DS_GetDocumentHubLogos";

		private readonly GQIStringArgument _bucketNameArgument = new GQIStringArgument("Bucket Name") { IsRequired = true };
		private readonly GQIStringArgument _nameFilterArgument = new GQIStringArgument("Name Filter");

		private GQIDMS _dms;
		private string _bucketName = String.Empty;
		private string _nameFilter = String.Empty;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Bucket"),
				new GQIStringColumn("File Name"),
				new GQIStringColumn("Icon"),
				new GQIStringColumn("Extension"),
				new GQIStringColumn("Directory"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				_bucketNameArgument,
				_nameFilterArgument,
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			try
			{
				if (String.IsNullOrWhiteSpace(_bucketName))
				{
					return new GQIPage(Array.Empty<GQIRow>());
				}

				IDocumentHubApiHelper helper = _dms.GetDocumentHubApiHelper();
				var bucket = GetBucketByName(helper, _bucketName);
				if (bucket == null)
				{
					return new GQIPage(Array.Empty<GQIRow>());
				}

				var docHubClient = _dms.GetDocHubClient();
				var files = docHubClient.Files.ReadFiles(bucket, config: null);
				var matchingFiles = files.Where(MatchesFilter).ToList();

				var rows = matchingFiles
					.OrderBy(file => file.GetFile(), StringComparer.OrdinalIgnoreCase)
					.Select(file => BuildRow(bucket.Name, file))
					.ToArray();

				return new GQIPage(rows);
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Array.Empty<GQIRow>());
			}
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_bucketName = args.GetArgumentValue(_bucketNameArgument) ?? String.Empty;
			_nameFilter = args.GetArgumentValue(_nameFilterArgument) ?? String.Empty;

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			return default;
		}

		private static GQIRow BuildRow(string bucketName, IDocHubFile file)
		{
			var fileName = file.GetFile() ?? String.Empty;
			var webPath = file.GetWebPath() ?? String.Empty;
			var filePath = file.GetFilePath() ?? String.Empty;
			var icon = NormalizePath(webPath, filePath);
			var extension = file.GetExtension() ?? String.Empty;

			return new GQIRow(
				new[]
				{
					new GQICell { Value = bucketName ?? String.Empty },
					new GQICell { Value = fileName },
					new GQICell { Value = icon },
					new GQICell { Value = extension },
					new GQICell { Value = GetDirectory(filePath, webPath) },
				});
		}

		private static string NormalizePath(string webPath, string filePath)
		{
			if (!String.IsNullOrWhiteSpace(webPath) &&
				Uri.TryCreate(webPath, UriKind.Absolute, out Uri uri) &&
				(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
			{
				return webPath.StartsWith("/", StringComparison.Ordinal) ? webPath : $"/{webPath}";
			}

			return filePath ?? String.Empty;
		}

		private static DocumentBucket GetBucketByName(IDocumentHubApiHelper helper, string bucketName)
		{
			if (helper == null || String.IsNullOrWhiteSpace(bucketName))
			{
				return null;
			}

			var normalizedName = bucketName.Trim();

			var bucket = helper.DocumentBuckets
				.Read(DocumentBucketExposers.Name.Equal(normalizedName))
				.FirstOrDefault();

			if (bucket != null)
			{
				return bucket;
			}

			return helper.DocumentBuckets
				.Read(new TRUEFilterElement<DocumentBucket>())
				.OrderByDescending(b => b.IsDefault)
				.ThenBy(b => b.Name ?? String.Empty, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault();
		}

		private static string GetDirectory(string filePath, string webPath)
		{
			var path = !String.IsNullOrWhiteSpace(webPath) ? webPath : filePath;
			if (String.IsNullOrWhiteSpace(path))
			{
				return String.Empty;
			}

			if (Uri.TryCreate(path, UriKind.Absolute, out Uri uri))
			{
				var uriPath = uri.AbsolutePath ?? String.Empty;
				return Path.GetDirectoryName(uriPath)?.Replace('\\', '/') ?? String.Empty;
			}

			return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? String.Empty;
		}

		private bool MatchesFilter(IDocHubFile file)
		{
			if (String.IsNullOrWhiteSpace(_nameFilter))
			{
				return true;
			}

			var fileName = file.GetFile() ?? String.Empty;
			return fileName.IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
