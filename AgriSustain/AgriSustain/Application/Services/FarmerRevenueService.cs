using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Revenue;
using AgriSustain.Application.Interfaces;

namespace AgriSustain.Application.Services;

public sealed class FarmerRevenueService(IFarmerRevenueRepository repository, RevenueAggregationRules rules)
{
    private static FeatureResult<T> Fail<T>(string code) => FeatureResult<T>.Fail(
        code == "MSG121" ? "FORBIDDEN" : code, FarmerBusinessMessages.Text(code));

    private async Task<FeatureResult<IReadOnlyList<FarmerRevenueEvent>>> Read(long actor, RevenueFilterRequest filter, CancellationToken ct)
    {
        if (!await repository.CanReadAsync(actor, ct)) return Fail<IReadOnlyList<FarmerRevenueEvent>>("MSG121");
        if (rules.ValidateDateRange(filter) is { } error) return Fail<IReadOnlyList<FarmerRevenueEvent>>(error);
        var asOf = filter.To!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7);
        return FeatureResult<IReadOnlyList<FarmerRevenueEvent>>.Ok(rules.Filter(
            await repository.ReadFinancialEventsAsync(actor, asOf < DateTime.UtcNow ? asOf : DateTime.UtcNow, ct), filter));
    }

    public async Task<FeatureResult<RevenueReportResponse>> GetRevenueAsync(long actor, RevenueFilterRequest filter, CancellationToken ct)
    {
        var read = await Read(actor, filter, ct);
        if (!read.IsSuccess) return new(default, read.Error);
        var all = read.Value!;
        var chart = all.GroupBy(x => x.Channel).Select(group => new
        { channel = group.Key, sales = group.Sum(x => x.Sales), receipts = group.Sum(x => x.Receipts), payout = group.Sum(x => x.Payout) }).ToList();
        return FeatureResult<RevenueReportResponse>.Ok(new(filter, rules.AggregateWithoutDoubleCounting(all),
            all.Skip((filter.Page - 1) * 10).Take(10).ToList(), all.Count, filter.Page, 10,
            chart, await repository.SeasonsAsync(actor, ct), "Ngày ghi nhận sự kiện tài chính (giờ Việt Nam)", "VND", DateTime.UtcNow,
            all.Count == 0 ? FarmerBusinessMessages.Text("MSG110") : null));
    }

    public async Task<FeatureResult<FarmerRevenueEvent>> GetTransactionDetailAsync(long actor, string id, CancellationToken ct)
    {
        if (!await repository.CanReadAsync(actor, ct)) return Fail<FarmerRevenueEvent>("MSG121");
        if (id.Length > 100) return Fail<FarmerRevenueEvent>("MSG120");
        var events = await repository.ReadFinancialEventsAsync(actor, DateTime.UtcNow, ct);
        var item = events.SingleOrDefault(x => x.Id == id);
        return item == null ? Fail<FarmerRevenueEvent>("MSG121") : FeatureResult<FarmerRevenueEvent>.Ok(item);
    }

    public async Task<FeatureResult<RevenueExport>> ExportRevenueAsync(long actor, RevenueFilterRequest filter, string format, CancellationToken ct)
    {
        if (format is not ("csv" or "xlsx")) return Fail<RevenueExport>("MSG120");
        var read = await Read(actor, filter, ct);
        if (!read.IsSuccess) return new(default, read.Error);
        var events = read.Value!;
        var totals = rules.AggregateWithoutDoubleCounting(events);
        List<string[]> rows =
        [
            ["Báo cáo doanh thu nông trại", "VND"],
            ["Từ ngày", filter.From.ToString()!, "Đến ngày", filter.To.ToString()!],
            ["Cơ sở ngày", "Ngày ghi nhận sự kiện tài chính (giờ Việt Nam)"],
            ["Thời điểm tạo", DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss")],
            ["Bộ lọc", $"Mùa vụ={filter.Season}; Kênh={filter.Channel}; Giai đoạn={filter.PaymentStage}; Trạng thái={filter.SettlementState}"],
            ["Tổng doanh số", N(totals.Sales), "Khoản thu xác minh", N(totals.Receipts), "Cọc giữ", N(totals.Deposits),
                "Hoàn tiền", N(totals.Refunds), "Hoa hồng", N(totals.Commission), "Đủ điều kiện", N(totals.Eligible),
                "Đang giữ", N(totals.Held), "Đã thực nhận", N(totals.Payout)],
            ["Mã", "Ngày ghi nhận", "Tham chiếu", "Loại", "Kênh", "Mùa vụ", "Giai đoạn", "Trạng thái", "Doanh số", "Khoản thu", "Cọc", "Hoàn", "Hoa hồng", "Đủ điều kiện", "Đang giữ", "Thực nhận", "Lý do giữ", "Mã chuyển khoản"]
        ];
        rows.AddRange(events.Select(x => new[] { x.Id, x.At.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"), x.Reference,
            x.Kind, x.Channel, x.Season, x.Stage, x.State, N(x.Sales), N(x.Receipts), N(x.Deposits), N(x.Refunds),
            N(x.Commission), N(x.Eligible), N(x.Held), N(x.Payout), x.HoldReason ?? "", x.TransferReference ?? "" }));
        byte[] content = format == "csv" ? Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
            string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv)))))).ToArray() : Excel(rows);
        return FeatureResult<RevenueExport>.Ok(new(content,
            format == "csv" ? "text/csv; charset=utf-8" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"doanh-thu-{filter.From:yyyyMMdd}-{filter.To:yyyyMMdd}.{format}"));
    }

    private static string N(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Csv(string text)
    {
        if (text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+') || text.TrimStart().StartsWith('-')
            || text.TrimStart().StartsWith('@')) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static byte[] Excel(List<string[]> rows)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string text)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(text);
            }
            Entry("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Entry("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Entry("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Doanh thu" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Entry("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
                """);
            using var xml = XmlWriter.Create(zip.CreateEntry("xl/worksheets/sheet1.xml").Open(), new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            xml.WriteStartElement("worksheet", ns);
            xml.WriteStartElement("sheetData", ns);
            foreach (var row in rows)
            {
                xml.WriteStartElement("row", ns);
                foreach (string value in row)
                {
                    xml.WriteStartElement("c", ns); xml.WriteAttributeString("t", "inlineStr");
                    xml.WriteStartElement("is", ns); xml.WriteElementString("t", ns, value);
                    xml.WriteEndElement(); xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement(); xml.WriteEndElement();
        }
        return output.ToArray();
    }
}
