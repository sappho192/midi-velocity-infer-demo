namespace mvi2demo.Services
{
    public interface IBufferedFileUploadService
    {
        Task<(bool, string)> UploadFile(IFormFile file);
    }
}
