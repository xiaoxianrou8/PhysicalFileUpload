using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using temp1.Models;
using temp1.Util;
using static temp1.Filters.ModelBinding;

namespace temp1.Controllers
{
    public class HomeController : Controller
    {
        private static readonly FormOptions _defaultFormOptions = new FormOptions();
        private readonly long _fileSizeLimit;
        private readonly ILogger<HomeController> _logger;
        private readonly string[] _permittedExtensions = { ".txt" };
        private readonly string _targetFilePath;


        public HomeController(ILogger<HomeController> logger, IConfiguration config)
        {
            _logger = logger;
            _fileSizeLimit = config.GetValue<long>("FileSizeLimit");

            // To save physical files to a path provided by configuration:
            _targetFilePath = config.GetValue<string>("StoredFilesPath");
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        //[DisableFormValueModelBinding]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhysical()
        {
            if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
            {
                ModelState.AddModelError("File",
                    $"The request couldn't be processed (Error 1).");
                // Log error

                return BadRequest(ModelState);
            }

            var boundary = MultipartRequestHelper.GetBoundary(
                MediaTypeHeaderValue.Parse(Request.ContentType),
                _defaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);
            var section = await reader.ReadNextSectionAsync();

            while (section != null)
            {
                var hasContentDispositionHeader =
                    ContentDispositionHeaderValue.TryParse(
                        section.ContentDisposition, out var contentDisposition);

                if (hasContentDispositionHeader)
                {
                    // This check assumes that there's a file
                    // present without form data. If form data
                    // is present, this method immediately fails
                    // and returns the model error.
                    if (!MultipartRequestHelper
                        .HasFileContentDisposition(contentDisposition))
                    {
                        ModelState.AddModelError("File",
                            $"The request couldn't be processed (Error 2).");
                        // Log error

                        return BadRequest(ModelState);
                    }
                    else
                    {
                        // Don't trust the file name sent by the client. To display
                        // the file name, HTML-encode the value.
                        var trustedFileNameForDisplay = WebUtility.HtmlEncode(
                                contentDisposition.FileName.Value);
                        var trustedFileNameForFileStorage = Path.GetRandomFileName();

                        // **WARNING!**
                        // In the following example, the file is saved without
                        // scanning the file's contents. In most production
                        // scenarios, an anti-virus/anti-malware scanner API
                        // is used on the file before making the file available
                        // for download or for use by other systems. 
                        // For more information, see the topic that accompanies 
                        // this sample.

                        var streamedFileContent = await FileHelpers.ProcessStreamedFile(
                            section, contentDisposition, ModelState,
                            _permittedExtensions, _fileSizeLimit);

                        if (!ModelState.IsValid)
                        {
                            return BadRequest(ModelState);
                        }

                        using (var targetStream = System.IO.File.Create(
                            Path.Combine(_targetFilePath, trustedFileNameForFileStorage)))
                        {
                            await targetStream.WriteAsync(streamedFileContent);

                            _logger.LogInformation(
                                "Uploaded file '{TrustedFileNameForDisplay}' saved to " +
                                "'{TargetFilePath}' as {TrustedFileNameForFileStorage}",
                                trustedFileNameForDisplay, _targetFilePath,
                                trustedFileNameForFileStorage);
                        }
                    }
                }

                // Drain any remaining section body that hasn't been consumed and
                // read the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            return Created(nameof(HomeController), null);
        }
        //[HttpPost]
        //public async Task<bool> HttpFileReceive()
        //{
        //    var tSaveDir = Path.Combine(Environment.CurrentDirectory, "wwwroot", "UserFiles");
        //    if (!Directory.Exists(tSaveDir))
        //    {
        //        Directory.CreateDirectory(tSaveDir);
        //    }
        //    //获取请求边界
        //    var boundary = Request.GetMultipartBoundary();

        //    if (string.IsNullOrEmpty(boundary))
        //    {
        //        return false;
        //    }
        //    //准备文件保存路径
        //    var filePath = string.Empty;
        //    //准备viewmodel缓冲
        //    var accumulator = new KeyValueAccumulator();
        //    var reader = new MultipartReader(boundary, Request.Body);
        //    try
        //    {
        //        var section = await reader.ReadNextSectionAsync();
        //        while (section != null)
        //        {
        //            var header = section.GetContentDispositionHeader();
        //            if (header.FileName!=null || header.FileNameStar!=null)
        //            {
        //                var fileSection = section.AsFileSection();
        //                var fileName = fileSection.FileName;
        //                filePath = Path.Combine(tSaveDir, fileName);
        //                if (System.IO.File.Exists(filePath))
        //                {
        //                    return false;
        //                }
        //                accumulator.Append("mimeType", fileSection.Section.ContentType);
        //                accumulator.Append("fileName", fileName);
        //                accumulator.Append("filePath", filePath);
        //                using (var writeStream = System.IO.File.Create(filePath))
        //                {
        //                    const int bufferSize = 1024;
        //                    await fileSection.FileStream.CopyToAsync(writeStream, bufferSize);
        //                }
        //            }
        //            else
        //            {
        //                var formDataSection = section.AsFormDataSection();
        //                var name = formDataSection.Name;
        //                var value = await formDataSection.GetValueAsync();
        //                accumulator.Append(name, value);
        //            }
        //            section = await reader.ReadNextSectionAsync();
        //        }
        //    }
        //    catch (System.Exception e)
        //    {
        //        if (System.IO.File.Exists(filePath))
        //        {
        //            System.IO.File.Delete(filePath);
        //        }
        //        return false;
        //    }
        //    var formValueProvider = new FormValueProvider(
        //     BindingSource.Form,
        //     new FormCollection(accumulator.GetResults()),
        //     CultureInfo.CurrentCulture);
        //    return true;
        //}

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
