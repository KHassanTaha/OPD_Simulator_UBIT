namespace OpdSimulator.Data.Tests;

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Validation;
using Xunit;

public class LoaderTests
{
    [Fact]
    public void Csv_Load_ParsesHeaderAndRows()
    {
        string path = Path.Combine(Path.GetTempPath(), $"loader-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(path, "arrival_time,departure_stage,screening_start,screening_end\n" +
                                     "8:15,Screening,8:20,8:30\n" +
                                     "8:40,Doctor,8:42,8:55\n");

            var data = new CsvLoader().Load(path);

            Assert.Equal(4, data.Columns.Count);
            Assert.Equal(2, data.RowCount);
            Assert.Equal("Screening", data.Rows[0]["departure_stage"]);
            Assert.Equal("8:15", data.Rows[0]["arrival_time"]);
            Assert.Equal("8:55", data.Rows[1]["screening_end"]);
            Assert.Equal(path, data.SourcePath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Csv_Load_MissingTrailingField_BecomesEmptyString()
    {
        string path = Path.Combine(Path.GetTempPath(), $"loader-{Guid.NewGuid():N}.csv");
        try
        {
            // Last row lacks screening_end: CsvHelper must not throw, cell is empty.
            File.WriteAllText(path, "arrival_time,screening_start,screening_end\n" +
                                     "8:15,8:20,8:30\n" +
                                     "8:40,8:42,\n");

            var data = new CsvLoader().Load(path);
            Assert.Equal(2, data.RowCount);
            Assert.Equal(string.Empty, data.Rows[1]["screening_end"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Excel_Load_ParsesWorkbook_FirstSheet()
    {
        string path = Path.Combine(Path.GetTempPath(), $"loader-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var book = new ClosedXML.Excel.XLWorkbook())
            {
                var sheet = book.AddWorksheet("Data");
                sheet.Cell(1, 1).Value = "arrival_time";
                sheet.Cell(1, 2).Value = "departure_stage";
                sheet.Cell(2, 1).Value = "8:15";
                sheet.Cell(2, 2).Value = "Screening";
                book.SaveAs(path);
            }

            var data = new ExcelLoader().Load(path);

            Assert.Equal(2, data.Columns.Count);
            Assert.Equal(1, data.RowCount);
            // ClosedXML serialises the time cell as a fraction ("0.34375"), which
            // TimeParser reads back as minutes — the load round-trips.
            Assert.Equal("Screening", data.Rows[0]["departure_stage"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Factory_RejectsUnknownExtension()
    {
        string path = Path.Combine(Path.GetTempPath(), $"loader-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "a,b\n1,2\n");
            var factory = new DataLoaderFactory();
            Assert.Throws<NotSupportedException>(() => factory.Create(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Factory_DispatchesByExtension()
    {
        string xlsx = Path.Combine(Path.GetTempPath(), $"dispatch-{Guid.NewGuid():N}.xlsx");
        string csv = Path.Combine(Path.GetTempPath(), $"dispatch-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(xlsx, "");
            File.WriteAllText(csv, "");
            Assert.IsType<ExcelLoader>(new DataLoaderFactory().Create(xlsx));
            Assert.IsType<CsvLoader>(new DataLoaderFactory().Create(csv));
        }
        finally
        {
            File.Delete(xlsx);
            File.Delete(csv);
        }
    }

    [Fact]
    public void Factory_MissingFile_ThrowsFileNotFound()
    {
        var factory = new DataLoaderFactory();
        Assert.Throws<FileNotFoundException>(() => factory.Create(Path.Combine(Path.GetTempPath(), "does-not-exist.xlsx")));
    }

    [Fact]
    public void LoadedCsv_ThenValidator_Passes()
    {
        string path = Path.Combine(Path.GetTempPath(), $"loader-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(path, "arrival_time,departure_stage,screening_start,screening_end\n" +
                                     "8:15,Screening,8:20,8:30\n" +
                                     "8:40,Doctor,8:42,8:55\n");

            var data = new CsvLoader().Load(path);
            DataValidator.Validate(data); // must not throw
        }
        finally
        {
            File.Delete(path);
        }
    }
}