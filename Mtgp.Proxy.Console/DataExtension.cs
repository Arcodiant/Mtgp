using Mtgp.Messages;

namespace Mtgp.Proxy;

internal class DataExtension(IEnumerable<IDataScheme> dataSchemes)
	: IProxyExtension
{
	private readonly Dictionary<string, IDataScheme> dataSchemesByPath = dataSchemes.ToDictionary(scheme => scheme.Name);

	private static string GetPath(Uri uri) => $"/{uri.Host}{uri.AbsolutePath}";

	public void RegisterMessageHandlers(ProxyController proxy)
	{
		proxy.RegisterMessageHandler<GetDataRequest>(this.HandleGetDataRequest);
		proxy.RegisterMessageHandler<SetDataRequest>(this.HandleSetDataRequest);
	}

	private MtgpResponse HandleGetDataRequest(GetDataRequest request)
	{
		var uri = new Uri(request.Uri);

		if (!this.dataSchemesByPath.TryGetValue(uri.Scheme, out var dataScheme))
		{
			return new GetDataResponse(request.Id, null);
		}

		return new GetDataResponse(request.Id, dataScheme.Get(GetPath(uri)));
	}

	private MtgpResponse HandleSetDataRequest(SetDataRequest request)
	{
		var uri = new Uri(request.Uri);

		if (!this.dataSchemesByPath.TryGetValue(uri.Scheme, out var dataScheme))
		{
			return new MtgpResponse(request.Id, "invalidScheme");
		}

		if (dataScheme.CanWrite)
		{
			dataScheme.Set(GetPath(uri), request.Value, request.ExpiryTimestamp);

			return new MtgpResponse(request.Id, "ok");
		}
		else
		{
			return new MtgpResponse(request.Id, "readOnly");
		}
	}
}