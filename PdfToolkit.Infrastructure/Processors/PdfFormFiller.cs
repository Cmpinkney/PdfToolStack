using iTextSharp.text.pdf;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Interfaces;
using DomainFormField = PdfToolkit.Domain.Entities.PdfFormField;

namespace PdfToolkit.Infrastructure.Processors
{
    public class PdfFormFiller : IPdfProcessor
    {
        public ToolType ToolType => ToolType.FillPdfForm;

        private readonly Dictionary<string, string> _fieldValues;

        public PdfFormFiller(
            Dictionary<string, string>? fieldValues = null)
        {
            _fieldValues = fieldValues
                ?? new Dictionary<string, string>();
        }

        public async Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var inputStream =
                    new MemoryStream(fileBytes);
                using var outputStream = new MemoryStream();

                var reader = new PdfReader(inputStream);
                var stamper = new PdfStamper(
                    reader, outputStream);

                var fields = stamper.AcroFields;

                foreach (var kvp in _fieldValues)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    fields.SetField(kvp.Key, kvp.Value);
                }

                stamper.FormFlattening = true;
                stamper.Close();
                reader.Close();

                return outputStream.ToArray();

            }, cancellationToken);
        }

        public List<DomainFormField> DetectFields(
            byte[] fileBytes)
        {
            var result = new List<DomainFormField>();

            using var stream = new MemoryStream(fileBytes);
            var reader = new PdfReader(stream);
            var fields = reader.AcroFields;

            foreach (var field in fields.Fields)
            {
                var fieldType = fields.GetFieldType(field.Key);

                var pdfField = new DomainFormField
                {
                    Name = field.Key,
                    Value = fields.GetField(field.Key),
                    FieldType = GetFieldTypeName(fieldType)
                };

                if (fieldType == AcroFields.FIELD_TYPE_LIST ||
                    fieldType == AcroFields.FIELD_TYPE_COMBO)
                {
                    var options = fields.GetListOptionExport(
                        field.Key);
                    if (options != null)
                        pdfField.Options.AddRange(options);
                }

                result.Add(pdfField);
            }

            reader.Close();
            return result;
        }

        private static string GetFieldTypeName(int fieldType)
        {
            return fieldType switch
            {
                AcroFields.FIELD_TYPE_CHECKBOX => "checkbox",
                AcroFields.FIELD_TYPE_COMBO => "dropdown",
                AcroFields.FIELD_TYPE_LIST => "list",
                AcroFields.FIELD_TYPE_TEXT => "text",
                AcroFields.FIELD_TYPE_SIGNATURE => "signature",
                _ => "text"
            };
        }
    }
}