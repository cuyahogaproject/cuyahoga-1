using System.Net;

namespace Cuyahoga.Web.Core.Services
{
	/// <summary>
	/// Asks the WebForms site for things that only it can answer (who is logged in, the placeholders of a template),
	/// on behalf of the current request: the request goes out with the cookies of the browser.
	/// </summary>
	public class LegacySiteClient
	{
		public const string HttpClientName = "LegacySite";

		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IHttpContextAccessor _httpContextAccessor;

		public LegacySiteClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
		{
			this._httpClientFactory = httpClientFactory;
			this._httpContextAccessor = httpContextAccessor;
		}

		/// <summary>
		/// GETs a path of the WebForms site (relative to LegacySiteUrl) and returns the status code and the body.
		/// </summary>
		public async Task<(HttpStatusCode StatusCode, string Body)> GetAsync(string path)
		{
			HttpContext context = this._httpContextAccessor.HttpContext
				?? throw new InvalidOperationException("The WebForms site can only be asked during a request.");
			HttpClient client = this._httpClientFactory.CreateClient(HttpClientName);
			// The WebForms site redirects the first request after it starts to the same url (module loading), so
			// follow that once.
			for (int attempt = 0; attempt < 2; attempt++)
			{
				using var request = new HttpRequestMessage(HttpMethod.Get, path);
				request.Headers.TryAddWithoutValidation("Cookie", context.Request.Headers.Cookie.ToString());
				using HttpResponseMessage response = await client.SendAsync(request, context.RequestAborted);

				// Forms authentication renews the cookie halfway its lifetime (sliding expiration).
				if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> setCookies))
				{
					foreach (string setCookie in setCookies)
					{
						context.Response.Headers.Append("Set-Cookie", setCookie);
					}
				}
				if (response.StatusCode == HttpStatusCode.Found)
				{
					continue;
				}
				return (response.StatusCode, await response.Content.ReadAsStringAsync(context.RequestAborted));
			}
			throw new InvalidOperationException($"{path} of the WebForms site keeps redirecting.");
		}
	}
}
