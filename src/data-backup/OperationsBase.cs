using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataBackup
{
    [SuppressMessage("CodeAnalysis", "CA1822", Justification = "Prefer the clarity of instance methods")]
    public abstract class OperationsBase
    {
        public const string BackupExtension = ".jsonbak";

        public ICommonOptions Options { get; }

        public DirectoryInfo Directory { get; private set; }

        protected OperationsBase(ICommonOptions options)
        {
            Options = options;
            Directory = new DirectoryInfo(Options.Folder ?? ".");

            if (!Directory.Exists)
            {
                throw new InvalidDataException($"Missing folder {Directory.Name}");
            }
        }

        public abstract Task<bool> ExecuteAsync();

        protected IReadOnlyCollection<DataFile> GetFiles()
        {
            return Directory.GetFiles($"*{BackupExtension}", new EnumerationOptions { RecurseSubdirectories = false })
                .Select(x => new DataFile(x))
                .ToList();
        }

        protected DataFile CreateBackupFile(string entityName)
        {
            return new DataFile(new FileInfo(Path.Combine(Directory.FullName, entityName + BackupExtension)));
        }

        protected string GetPropertyValue(JObject entity, string propertyName, string defaultValue = null)
        {
            var prop = entity.GetValue(propertyName, StringComparison.OrdinalIgnoreCase);

            return prop?.Value<string>() ?? defaultValue;
        }

        protected class DataFile
        {
            private readonly FileInfo file;

            public string EntityName { get; set; }

            public DataFile(FileInfo file)
            {
                this.file = file;
                EntityName = Path.GetFileNameWithoutExtension(file.Name);
            }

            public async Task<JArray> ReadAsync()
            {
                using (var stream = file.OpenText())
                {
                    return await JArray.LoadAsync(new JsonTextReader(stream)).ConfigureAwait(false);
                }
            }

            public async Task WriteAsync(JArray data)
            {
                using (var stream = file.CreateText())
                {
                    var writer = new JsonTextWriter(stream) { Formatting = Formatting.Indented };

                    await data.WriteToAsync(writer).ConfigureAwait(false);
                }
            }
        }
    }
}
