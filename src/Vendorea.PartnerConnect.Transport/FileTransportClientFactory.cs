using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Transport.FileSystem;
using Vendorea.PartnerConnect.Transport.Interfaces;
using Vendorea.PartnerConnect.Transport.Sftp;

namespace Vendorea.PartnerConnect.Transport;

public class FileTransportClientFactory : IFileTransportClientFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public FileTransportClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IFileTransportClient CreateSftpClient()
    {
        return new SftpFileTransportClient(_loggerFactory.CreateLogger<SftpFileTransportClient>());
    }

    public IFileTransportClient CreateLocalFileClient()
    {
        return new LocalFileTransportClient(_loggerFactory.CreateLogger<LocalFileTransportClient>());
    }
}
