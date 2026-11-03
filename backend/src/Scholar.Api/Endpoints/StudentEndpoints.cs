using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Scholar.Api.Auth;
using Scholar.Application.Admissions;
using Scholar.Application.Common;
using Scholar.Application.Documents;
using Scholar.Application.Students;
using Scholar.Domain.Access;
using Scholar.Domain.Documents;

namespace Scholar.Api.Endpoints;

internal static class StudentEndpoints
{
    private static IResult Download(ExportFile file) => Results.File(file.Content, file.ContentType, file.FileName);

    private static ExportFormat Format(string? format) =>
        string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase) ? ExportFormat.Xlsx : ExportFormat.Csv;

    private static FileUpload ToUpload(IFormFile file) => new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);

    public static void MapStudentEndpoints(this IEndpointRouteBuilder api)
    {
        var students = api.MapGroup("/students").WithTags("Students");
        students.MapGet("/", ([AsParameters] StudentQuery q, StudentService s, CancellationToken ct) => s.ListAsync(q, ct))
            .RequirePermission(Permissions.Students.View);
        students.MapGet("/export", async ([AsParameters] StudentQuery q, string? format, StudentTransferService s, CancellationToken ct) =>
            Download(await s.ExportAsync(q, Format(format), ct))).RequirePermission(Permissions.Students.Export);
        students.MapGet("/import/template", (StudentTransferService s) => Download(s.Template())).RequirePermission(Permissions.Students.Import);
        students.MapPost("/import", async (IFormFile file, bool? commit, StudentTransferService s, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return await s.ImportAsync(stream, commit == true, ct);
            })
            .RequirePermission(Permissions.Students.Import).DisableAntiforgery()
            .WithSummary("Validate (commit=false) or import (commit=true) a CSV of students. All rows or none.");
        students.MapPost("/bulk/transfer", (BulkTransferRequest r, StudentService s, CancellationToken ct) => s.BulkTransferAsync(r, ct))
            .RequirePermission(Permissions.Students.Update).WithSummary("Move several students into one section, all or none.");
        students.MapGet("/{id:guid}", (Guid id, StudentService s, CancellationToken ct) => s.GetAsync(id, ct)).RequirePermission(Permissions.Students.View);
        students.MapPost("/", async (CreateStudentRequest r, StudentService s, CancellationToken ct) =>
        {
            var student = await s.CreateAsync(r, ct);
            return Results.Created($"/api/students/{student.Id}", student);
        }).RequirePermission(Permissions.Students.Create);
        students.MapPut("/{id:guid}", (Guid id, UpdateStudentRequest r, StudentService s, CancellationToken ct) => s.UpdateAsync(id, r, ct))
            .RequirePermission(Permissions.Students.Update);
        students.MapDelete("/{id:guid}", async (Guid id, StudentService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Students.Delete);
        students.MapPost("/{id:guid}/status", (Guid id, ChangeStudentStatusRequest r, StudentService s, CancellationToken ct) =>
            s.ChangeStatusAsync(id, r, ct)).RequirePermission(Permissions.Students.ManageStatus);
        students.MapPost("/{id:guid}/transfer", (Guid id, TransferSectionRequest r, StudentService s, CancellationToken ct) =>
            s.TransferSectionAsync(id, r, ct)).RequirePermission(Permissions.Students.Update);
        students.MapPost("/{id:guid}/guardians", (Guid id, GuardianLinkInput r, StudentService s, CancellationToken ct) =>
            s.AddGuardianAsync(id, r, ct)).RequirePermission(Permissions.Students.Update);
        students.MapPut("/{id:guid}/guardians/{linkId:guid}", (Guid id, Guid linkId, UpdateGuardianLinkRequest r, StudentService s,
            CancellationToken ct) => s.UpdateGuardianLinkAsync(id, linkId, r, ct)).RequirePermission(Permissions.Students.Update);
        students.MapDelete("/{id:guid}/guardians/{linkId:guid}", (Guid id, Guid linkId, StudentService s, CancellationToken ct) =>
            s.RemoveGuardianLinkAsync(id, linkId, ct)).RequirePermission(Permissions.Students.Update);
        students.MapPut("/{id:guid}/emergency-contacts", (Guid id, List<EmergencyContactInput> r, StudentService s, CancellationToken ct) =>
            s.SetEmergencyContactsAsync(id, r, ct)).RequirePermission(Permissions.Students.Update);
        students.MapGet("/{id:guid}/medical", (Guid id, StudentService s, CancellationToken ct) => s.GetMedicalAsync(id, ct))
            .RequirePermission(Permissions.Students.ViewMedical);
        students.MapPut("/{id:guid}/medical", (Guid id, MedicalProfileDto r, StudentService s, CancellationToken ct) => s.UpdateMedicalAsync(id, r, ct))
            .RequirePermission(Permissions.Students.ManageMedical);
        students.MapGet("/{id:guid}/documents", (Guid id, FileService f, CancellationToken ct) => f.ListAsync(FileOwners.Student, id, ct))
            .RequirePermission(Permissions.Students.ViewDocuments);
        students.MapPost("/{id:guid}/documents", (Guid id, IFormFile file, [FromForm] string category, [FromForm] string? notes, FileService f,
            CancellationToken ct) => f.UploadAsync(FileOwners.Student, id, category, ToUpload(file), notes, ct))
            .RequirePermission(Permissions.Students.ManageDocuments, Permissions.Students.Update).DisableAntiforgery();

        var guardians = api.MapGroup("/guardians").WithTags("Guardians");
        guardians.MapGet("/", ([AsParameters] GuardianQuery q, GuardianService s, CancellationToken ct) => s.ListAsync(q, ct))
            .RequirePermission(Permissions.Guardians.View);
        guardians.MapGet("/lookup", (string phone, GuardianService s, CancellationToken ct) => s.FindAsync(phone, ct))
            .RequirePermission(Permissions.Guardians.View).WithSummary("Find existing guardians by mobile number before creating a new one.");
        guardians.MapGet("/{id:guid}", (Guid id, GuardianService s, CancellationToken ct) => s.GetAsync(id, ct)).RequirePermission(Permissions.Guardians.View);
        guardians.MapPost("/", (SaveGuardianRequest r, GuardianService s, CancellationToken ct) => s.CreateAsync(r, ct))
            .RequirePermission(Permissions.Guardians.Create);
        guardians.MapPut("/{id:guid}", (Guid id, SaveGuardianRequest r, GuardianService s, CancellationToken ct) => s.UpdateAsync(id, r, ct))
            .RequirePermission(Permissions.Guardians.Update);
        guardians.MapDelete("/{id:guid}", async (Guid id, GuardianService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.Guardians.Delete);
        guardians.MapPost("/{id:guid}/portal", (Guid id, GuardianService s, CancellationToken ct) => s.EnablePortalAsync(id, ct))
            .RequirePermission(Permissions.Guardians.ManagePortal).WithSummary("Create a family-portal login for the guardian.");
        guardians.MapPost("/{id:guid}/portal/reset", (Guid id, GuardianService s, CancellationToken ct) => s.ResetPortalPasswordAsync(id, ct))
            .RequirePermission(Permissions.Guardians.ManagePortal)
            .WithSummary("Issue a new one-time family-portal password (shown once) and sign the parent out everywhere.");

        var files = api.MapGroup("/files").WithTags("Files");
        files.MapGet("/{id:guid}/content", async (Guid id, bool? download, FileService f, HttpContext http, CancellationToken ct) =>
        {
            var content = await f.OpenAsync(id, ct);
            var disposition = new ContentDispositionHeaderValue(download == true ? "attachment" : "inline");
            disposition.SetHttpFileName(content.FileName);
            http.Response.Headers.ContentDisposition = disposition.ToString();
            http.Response.Headers.CacheControl = "private, max-age=600";
            return Results.Stream(content.Content, content.ContentType, enableRangeProcessing: true);
        }).WithSummary("Stream a stored file after checking the caller may see the record it belongs to.");
        files.MapDelete("/{id:guid}", async (Guid id, FileService f, CancellationToken ct) =>
        {
            await f.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        api.MapGet("/search", (string? q, SearchService s, CancellationToken ct) => s.SearchAsync(q, ct)).WithTags("Search")
            .WithSummary("Best matches across students, guardians and applications for the command palette.");

        api.MapPost("/org/school/logo", (IFormFile file, FileService f, Application.Abstractions.ITenantContext tenant, CancellationToken ct) =>
                f.UploadAsync(FileOwners.School, tenant.SchoolId!.Value, FileCategories.Logo, ToUpload(file), null, ct))
            .WithTags("School configuration").RequirePermission(Permissions.School.Manage).DisableAntiforgery();
    }

    public static void MapAdmissionEndpoints(this IEndpointRouteBuilder api)
    {
        var admissions = api.MapGroup("/admissions").WithTags("Admissions").RequireModule(Domain.Platform.SchoolModules.Admissions);

        admissions.MapGet("/inquiries", ([AsParameters] InquiryQuery q, AdmissionService s, CancellationToken ct) => s.ListInquiriesAsync(q, ct))
            .RequirePermission(Permissions.Admissions.View);
        admissions.MapPost("/inquiries", (SaveInquiryRequest r, AdmissionService s, CancellationToken ct) => s.SaveInquiryAsync(null, r, ct))
            .RequirePermission(Permissions.Admissions.Create);
        admissions.MapPut("/inquiries/{id:guid}", (Guid id, SaveInquiryRequest r, AdmissionService s, CancellationToken ct) => s.SaveInquiryAsync(id, r, ct))
            .RequirePermission(Permissions.Admissions.Create);
        admissions.MapPost("/inquiries/{id:guid}/{action}", (Guid id, string action, [FromBody] InquiryActionRequest? r, AdmissionService s,
            CancellationToken ct) => s.SetInquiryStateAsync(id, action, r?.Reason, ct)).RequirePermission(Permissions.Admissions.Update);

        var applications = admissions.MapGroup("/applications");
        applications.MapGet("/", ([AsParameters] ApplicationQuery q, AdmissionService s, CancellationToken ct) => s.ListApplicationsAsync(q, ct))
            .RequirePermission(Permissions.Admissions.View);
        applications.MapGet("/pipeline", (Guid? academicYearId, AdmissionService s, CancellationToken ct) => s.PipelineAsync(academicYearId, ct))
            .RequirePermission(Permissions.Admissions.View);
        applications.MapGet("/{id:guid}", (Guid id, AdmissionService s, CancellationToken ct) => s.GetApplicationAsync(id, ct))
            .RequirePermission(Permissions.Admissions.View);
        applications.MapPost("/", async (CreateApplicationRequest r, AdmissionService s, CancellationToken ct) =>
        {
            var application = await s.CreateApplicationAsync(r, ct);
            return Results.Created($"/api/admissions/applications/{application.Id}", application);
        }).RequirePermission(Permissions.Admissions.Create);
        applications.MapPut("/{id:guid}", (Guid id, UpdateApplicationRequest r, AdmissionService s, CancellationToken ct) =>
            s.UpdateApplicationAsync(id, r, ct)).RequirePermission(Permissions.Admissions.Update);
        applications.MapPost("/{id:guid}/actions/{action}", (Guid id, string action, [FromBody] InquiryActionRequest? r, AdmissionService s,
            CancellationToken ct) => s.TransitionAsync(id, action, r?.Reason, ct)).RequirePermission(Permissions.Admissions.Update, Permissions.Admissions.Review);
        applications.MapPost("/{id:guid}/decision", (Guid id, DecisionRequest r, AdmissionService s, CancellationToken ct) => s.DecideAsync(id, r, ct))
            .RequirePermission(Permissions.Admissions.Decide);
        applications.MapPost("/{id:guid}/assessments", (Guid id, ScheduleAssessmentRequest r, AdmissionService s, CancellationToken ct) =>
            s.ScheduleAssessmentAsync(id, r, ct)).RequirePermission(Permissions.Admissions.Review);
        applications.MapPut("/{id:guid}/assessments/{assessmentId:guid}", (Guid id, Guid assessmentId, RecordAssessmentRequest r, AdmissionService s,
            CancellationToken ct) => s.RecordAssessmentAsync(id, assessmentId, r, ct)).RequirePermission(Permissions.Admissions.Review);
        applications.MapPost("/{id:guid}/enroll", (Guid id, EnrollApplicantRequest r, AdmissionService s, CancellationToken ct) => s.EnrollAsync(id, r, ct))
            .RequirePermission(Permissions.Admissions.Enroll);
        applications.MapGet("/{id:guid}/documents", (Guid id, FileService f, CancellationToken ct) => f.ListAsync(FileOwners.Application, id, ct))
            .RequirePermission(Permissions.Admissions.View);
        applications.MapPost("/{id:guid}/documents", (Guid id, IFormFile file, [FromForm] string category, [FromForm] string? notes, FileService f,
            CancellationToken ct) => f.UploadAsync(FileOwners.Application, id, category, ToUpload(file), notes, ct))
            .RequirePermission(Permissions.Admissions.Update).DisableAntiforgery();

        var publicApi = api.MapGroup("/public/schools/{code}").WithTags("Public").AllowAnonymous().RequireRateLimiting(AuthSetup.PublicRateLimit);
        publicApi.MapGet("/", (string code, PublicAdmissionService s, CancellationToken ct) => s.GetSchoolAsync(code, ct))
            .WithSummary("What a family needs to start an online application.");
        publicApi.MapPost("/applications", (string code, PublicApplicationRequest r, PublicAdmissionService s, CancellationToken ct) =>
            s.SubmitAsync(code, r, ct)).WithSummary("Submit an online admission application.");
    }
}

public sealed record InquiryActionRequest(string? Reason);
