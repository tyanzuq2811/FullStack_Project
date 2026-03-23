using IPM.Application.DTOs.Booking;
using IPM.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet("resources")]
    [Authorize(Roles = "Admin,ProjectManager,Subcontractor")]
    public async Task<IActionResult> GetResources(CancellationToken cancellationToken)
    {
        var result = await _bookingService.GetResourcesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "ProjectManager,Subcontractor")]
    public async Task<IActionResult> GetAllBookings(CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _bookingService.GetMyBookingsAsync(memberId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("availability")]
    [Authorize(Roles = "ProjectManager,Subcontractor")]
    public async Task<IActionResult> GetAvailability([FromQuery] AvailabilityRequest request, CancellationToken cancellationToken)
    {
        var result = await _bookingService.GetAvailabilityAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "ProjectManager,Subcontractor")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _bookingService.CreateBookingAsync(memberId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("recurring")]
    [Authorize(Roles = "ProjectManager,Subcontractor")]
    public async Task<IActionResult> CreateRecurringBooking([FromBody] RecurringBookingRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _bookingService.CreateRecurringBookingAsync(memberId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete]
    [Authorize(Roles = "ProjectManager,Subcontractor")]
    public async Task<IActionResult> CancelBooking([FromBody] CancelBookingRequest request, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _bookingService.CancelBookingAsync(memberId, request, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = "ProjectManager,Subcontractor")]
    public async Task<IActionResult> GetMyBookings(CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        var result = await _bookingService.GetMyBookingsAsync(memberId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/confirm-payment")]
    [Authorize(Roles = "ProjectManager,Subcontractor")]
    public async Task<IActionResult> ConfirmPayment(Guid id, CancellationToken cancellationToken)
    {
        var result = await _bookingService.ConfirmBookingPaymentAsync(id, cancellationToken);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    private Guid GetCurrentMemberId()
    {
        var memberIdClaim = User.Claims.FirstOrDefault(c => c.Type == "memberId")?.Value;
        return Guid.TryParse(memberIdClaim, out var memberId) ? memberId : Guid.Empty;
    }
}
