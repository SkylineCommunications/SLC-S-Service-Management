namespace SLC_Service_Management.IntegrationTests
{
	using System;
	using System.Net;
	using System.Net.Http;

	using Newtonsoft.Json.Linq;

	/// <summary>A buffered HTTP response with the details every API assertion message needs.</summary>
	public sealed class ApiResponse
	{
		private const int MaxBodyLength = 600;

		private ApiResponse(string method, Uri uri, HttpStatusCode statusCode, string body, string contentType)
		{
			Method = method;
			Uri = uri;
			StatusCode = statusCode;
			Body = body ?? String.Empty;
			ContentType = contentType;
		}

		public string Method { get; }

		public Uri Uri { get; }

		public HttpStatusCode StatusCode { get; }

		public string Body { get; }

		public string ContentType { get; }

		public static ApiResponse Get(HttpClient client, string relativeUrl)
		{
			using (var response = client.GetAsync(relativeUrl).GetAwaiter().GetResult())
			{
				string body = response.Content == null ? String.Empty : response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				string contentType = response.Content?.Headers.ContentType?.MediaType;
				return new ApiResponse("GET", response.RequestMessage?.RequestUri ?? client.BaseAddress, response.StatusCode, body, contentType);
			}
		}

		public static string Truncate(string text)
		{
			if (String.IsNullOrEmpty(text))
			{
				return "(empty body)";
			}

			text = text.Replace("\r", String.Empty).Replace("\n", " ");
			return text.Length <= MaxBodyLength ? text : text.Substring(0, MaxBodyLength) + "…";
		}

		/// <summary>Parses the body as JSON; returns <see langword="null"/> when it is not valid JSON.</summary>
		public JToken TryParseJson()
		{
			try
			{
				return String.IsNullOrWhiteSpace(Body) ? null : JToken.Parse(Body);
			}
			catch (Exception)
			{
				return null;
			}
		}

		public override string ToString()
		{
			return $"{Method} {Uri} -> HTTP {(int)StatusCode} {StatusCode} ({ContentType ?? "no content type"}): {Truncate(Body)}";
		}
	}
}
