namespace Vendorea.PartnerConnect.Transport.Interfaces;

public interface IFileTransportClientFactory
{
    IFileTransportClient CreateSftpClient();
    IFileTransportClient CreateLocalFileClient();
}
