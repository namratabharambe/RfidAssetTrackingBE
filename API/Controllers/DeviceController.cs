using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers
{
    [ApiController]
    [Route("api/device")]
    public class DeviceController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeviceController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> DeviceLogin([FromBody] DeviceLoginRequest request, CancellationToken cancellationToken)
        {
            var readerRepo = _unitOfWork.Repository<Reader>();
            var handheldRepo = _unitOfWork.Repository<HandheldDevice>();

            var readers = await readerRepo.GetFilteredAsync(r => r.IpAddress == request.DeviceSerialOrIp, cancellationToken);
            if (readers.Any())
            {
                var r = readers.First();
                r.Status = DeviceStatus.Online;
                readerRepo.Update(r);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Ok(new { Token = "device_token_" + r.Id, DeviceType = "Reader", Id = r.Id });
            }

            var handhelds = await handheldRepo.GetFilteredAsync(h => h.DeviceSerial == request.DeviceSerialOrIp, cancellationToken);
            if (handhelds.Any())
            {
                var h = handhelds.First();
                h.Status = DeviceStatus.Online;
                handheldRepo.Update(h);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Ok(new { Token = "device_token_" + h.Id, DeviceType = "Handheld", Id = h.Id });
            }

            return Unauthorized("Device serial or IP not registered.");
        }

        [HttpPost("heartbeat")]
        [AllowAnonymous]
        public async Task<IActionResult> DeviceHeartbeat([FromBody] DeviceHeartbeatRequest request, CancellationToken cancellationToken)
        {
            var readerRepo = _unitOfWork.Repository<Reader>();
            var handheldRepo = _unitOfWork.Repository<HandheldDevice>();

            var readers = await readerRepo.GetFilteredAsync(r => r.Id == request.DeviceId, cancellationToken);
            if (readers.Any())
            {
                var r = readers.First();
                r.Status = DeviceStatus.Online;
                readerRepo.Update(r);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Ok(new { Status = "Online" });
            }

            var handhelds = await handheldRepo.GetFilteredAsync(h => h.Id == request.DeviceId, cancellationToken);
            if (handhelds.Any())
            {
                var h = handhelds.First();
                h.Status = DeviceStatus.Online;
                handheldRepo.Update(h);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Ok(new { Status = "Online" });
            }

            return NotFound("Device not found.");
        }

        [HttpPost("upload-scan")]
        [AllowAnonymous]
        public async Task<IActionResult> UploadScan([FromBody] UploadScanDto uploadScan, CancellationToken cancellationToken)
        {
            var sessionRepo = _unitOfWork.Repository<ScanSession>();
            var scanEventRepo = _unitOfWork.Repository<ScanEvent>();

            var sessions = await sessionRepo.GetFilteredAsync(s => 
                s.IsRunning && 
                (s.ReaderId == uploadScan.ReaderId || s.HandheldDeviceId == uploadScan.HandheldDeviceId), 
                cancellationToken);

            var session = sessions.FirstOrDefault();
            if (session == null)
            {
                session = new ScanSession
                {
                    Id = Guid.NewGuid(),
                    SessionName = $"Ad-hoc Session {DateTime.UtcNow:yyyyMMdd}",
                    StartTime = DateTime.UtcNow,
                    ReaderId = uploadScan.ReaderId,
                    HandheldDeviceId = uploadScan.HandheldDeviceId,
                    IsRunning = true
                };
                await sessionRepo.AddAsync(session, cancellationToken);
            }

            var scanEvent = new ScanEvent
            {
                Id = Guid.NewGuid(),
                ScanSessionId = session.Id,
                EpcCode = uploadScan.EpcCode,
                TidCode = uploadScan.TidCode,
                Timestamp = uploadScan.Timestamp == default ? DateTime.UtcNow : uploadScan.Timestamp,
                Rssi = uploadScan.Rssi,
                AntennaIndex = uploadScan.AntennaIndex,
                ReaderId = uploadScan.ReaderId,
                HandheldDeviceId = uploadScan.HandheldDeviceId,
                Status = ScanStatus.Matched
            };

            await scanEventRepo.AddAsync(scanEvent, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(new { Message = "Scan uploaded successfully.", ScanEventId = scanEvent.Id });
        }

        [HttpPost("upload-batch")]
        [AllowAnonymous]
        public async Task<IActionResult> UploadBatch([FromBody] List<UploadScanDto> scans, CancellationToken cancellationToken)
        {
            if (scans == null || !scans.Any())
                return BadRequest("No scans provided.");

            var sessionRepo = _unitOfWork.Repository<ScanSession>();
            var scanEventRepo = _unitOfWork.Repository<ScanEvent>();

            var readerId = scans.First().ReaderId;
            var handheldId = scans.First().HandheldDeviceId;

            var sessions = await sessionRepo.GetFilteredAsync(s => 
                s.IsRunning && 
                (s.ReaderId == readerId || s.HandheldDeviceId == handheldId), 
                cancellationToken);

            var session = sessions.FirstOrDefault();
            if (session == null)
            {
                session = new ScanSession
                {
                    Id = Guid.NewGuid(),
                    SessionName = $"Batch Session {DateTime.UtcNow:yyyyMMdd}",
                    StartTime = DateTime.UtcNow,
                    ReaderId = readerId,
                    HandheldDeviceId = handheldId,
                    IsRunning = true
                };
                await sessionRepo.AddAsync(session, cancellationToken);
            }

            foreach (var scan in scans)
            {
                var scanEvent = new ScanEvent
                {
                    Id = Guid.NewGuid(),
                    ScanSessionId = session.Id,
                    EpcCode = scan.EpcCode,
                    TidCode = scan.TidCode,
                    Timestamp = scan.Timestamp == default ? DateTime.UtcNow : scan.Timestamp,
                    Rssi = scan.Rssi,
                    AntennaIndex = scan.AntennaIndex,
                    ReaderId = readerId,
                    HandheldDeviceId = handheldId,
                    Status = ScanStatus.Matched
                };

                await scanEventRepo.AddAsync(scanEvent, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Ok(new { Message = $"{scans.Count} scans processed successfully." });
        }

        [HttpPost("start-session")]
        [AllowAnonymous]
        public async Task<IActionResult> StartSession([FromBody] CreateScanSessionDto createDto, CancellationToken cancellationToken)
        {
            var sessionRepo = _unitOfWork.Repository<ScanSession>();
            
            var activeSessions = await sessionRepo.GetFilteredAsync(s => 
                s.IsRunning && 
                ((createDto.ReaderId != null && s.ReaderId == createDto.ReaderId) || 
                 (createDto.HandheldDeviceId != null && s.HandheldDeviceId == createDto.HandheldDeviceId)), 
                cancellationToken);

            foreach (var s in activeSessions)
            {
                s.IsRunning = false;
                s.EndTime = DateTime.UtcNow;
                sessionRepo.Update(s);
            }

            var session = new ScanSession
            {
                Id = Guid.NewGuid(),
                SessionName = createDto.SessionName,
                StartTime = DateTime.UtcNow,
                ReaderId = createDto.ReaderId,
                HandheldDeviceId = createDto.HandheldDeviceId,
                IsRunning = true
            };

            await sessionRepo.AddAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(new { Message = "Scan session started.", SessionId = session.Id });
        }

        [HttpPost("end-session/{sessionId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> EndSession(Guid sessionId, CancellationToken cancellationToken)
        {
            var sessionRepo = _unitOfWork.Repository<ScanSession>();
            var session = await sessionRepo.GetByIdAsync(sessionId, cancellationToken);

            if (session == null)
                return NotFound("Session not found.");

            session.IsRunning = false;
            session.EndTime = DateTime.UtcNow;
            sessionRepo.Update(session);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Ok(new { Message = "Scan session ended." });
        }

        [HttpGet("config")]
        [AllowAnonymous]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                BufferTimeSeconds = 10,
                PowerLevelDbm = 30,
                BuzzerOnScan = true,
                HeartbeatIntervalSeconds = 60
            });
        }

        [HttpGet("readers")]
        [AllowAnonymous]
        public async Task<IActionResult> GetReaders(CancellationToken cancellationToken)
        {
            var readers = await _unitOfWork.Repository<Reader>().GetAllAsync(cancellationToken);
            return Ok(readers.Select(r => new { r.Id, r.Name, r.IpAddress }));
        }

        [HttpGet("sites")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSites(CancellationToken cancellationToken)
        {
            var sites = await _unitOfWork.Repository<Site>().GetAllAsync(cancellationToken);
            return Ok(sites.Select(s => new { s.Id, s.Name, s.Code }));
        }
    }

    public record DeviceLoginRequest(string DeviceSerialOrIp);
    public record DeviceHeartbeatRequest(Guid DeviceId);
}
