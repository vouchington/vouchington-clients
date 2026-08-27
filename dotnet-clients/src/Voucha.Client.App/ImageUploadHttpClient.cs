namespace Voucha.Client.App;

/// An unpinned HttpClient for pre-signed image uploads whose destination hosts are not the pinned API hosts.
public sealed class ImageUploadHttpClient : HttpClient
{
}
