namespace mvi2demo.Services
{
    public class BufferedFileUploadLocalService : IBufferedFileUploadService
    {
        public async Task<(bool, string)> UploadFile(IFormFile file)
        {
            try
            {
                if (file.Length > 0)
                {
                    string folderHash = Guid.NewGuid().ToString();
                    string folderPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "UploadedFiles", folderHash));
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    string filePath = Path.Combine(folderPath, file.FileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    return (true, filePath);
                }
                else
                {
                    return (false, string.Empty);
                }
            }
            catch (Exception ex)
            {
                //throw new Exception("File Copy Failed", ex);
                return (false, string.Empty);
            }
        }
    }
}
