using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    public class AssetIssuancesController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public AssetIssuancesController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        private Guid? CurrentUserSiteId
        {
            get
            {
                if (Request.Headers.TryGetValue("X-Site-Id", out var hVal) && Guid.TryParse(hVal.FirstOrDefault(), out var hGuid) && hGuid != Guid.Empty)
                    return hGuid;

                var claim = User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out var g) && g != Guid.Empty);
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        private Guid? CurrentUserWarehouseId
        {
            get
            {
                if (Request.Headers.TryGetValue("X-Warehouse-Id", out var hVal) && Guid.TryParse(hVal.FirstOrDefault(), out var hGuid) && hGuid != Guid.Empty)
                    return hGuid;

                var claim = User.Claims
                    .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out var g) && g != Guid.Empty);
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        /// <summary>
        /// Option A: Dedicated Material Issue Endpoint
        /// POST /api/assets/issue
        /// </summary>
        [HttpPost("api/assets/issue")]
        public async Task<ActionResult<AssetIssuanceDto>> IssueMaterial([FromBody] IssueMaterialRequestDto request, CancellationToken cancellationToken)
        {
            return await ProcessMaterialIssueAsync(request.AssetId, request, cancellationToken);
        }

        /// <summary>
        /// Option B: Asset Update Endpoint
        /// PUT /api/assets/{id}/issue
        /// </summary>
        [HttpPut("api/assets/{id:guid}/issue")]
        public async Task<ActionResult<AssetIssuanceDto>> IssueMaterialById(Guid id, [FromBody] IssueMaterialRequestDto request, CancellationToken cancellationToken)
        {
            return await ProcessMaterialIssueAsync(id, request, cancellationToken);
        }

        /// <summary>
        /// Report Endpoint: List all material issuance records with site, asset, and contractor filters.
        /// GET /api/assets/issuances OR GET /api/assets/issue/report
        /// </summary>
        [HttpGet("api/assets/issuances")]
        [HttpGet("api/assets/issue/report")]
        public async Task<ActionResult<IEnumerable<AssetIssuanceDto>>> GetIssuanceReport(
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            [FromQuery] Guid? assetId = null,
            [FromQuery] string? contractor = null,
            [FromQuery] string? issuedToPerson = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            CancellationToken cancellationToken = default)
        {
            var targetSiteId = siteId ?? CurrentUserSiteId;
            var targetWhId = warehouseId ?? CurrentUserWarehouseId;
            var repo = _unitOfWork.Repository<AssetIssuance>();

            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                null,
                x => (!x.IsDeleted) &&
                     (!targetSiteId.HasValue || x.SiteId == targetSiteId.Value) &&
                     (!targetWhId.HasValue || (x.Asset != null && x.Asset.WarehouseId == targetWhId.Value)) &&
                     (!assetId.HasValue || x.AssetId == assetId.Value) &&
                     (string.IsNullOrEmpty(contractor) || x.Contractor.Contains(contractor)) &&
                     (string.IsNullOrEmpty(issuedToPerson) || x.IssuedToPerson.Contains(issuedToPerson)) &&
                     (!fromDate.HasValue || x.IssuedDate >= fromDate.Value) &&
                     (!toDate.HasValue || x.IssuedDate <= toDate.Value),
                q => q.OrderByDescending(x => x.IssuedDate),
                cancellationToken,
                x => x.Asset);

            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(_mapper.Map<List<AssetIssuanceDto>>(items));
        }

        private async Task<ActionResult<AssetIssuanceDto>> ProcessMaterialIssueAsync(Guid assetId, IssueMaterialRequestDto request, CancellationToken cancellationToken)
        {
            // 1. Fetch Asset
            var asset = await _unitOfWork.Repository<Asset>().GetByIdAsync(assetId, cancellationToken);
            if (asset == null)
            {
                return NotFound(new { message = $"Asset with ID '{assetId}' not found." });
            }

            // Step 1: Calculate Current Balance Stock
            decimal totalEntryQty = asset.EntryQty ?? 0;
            if (totalEntryQty <= 0)
            {
                // Fallback: parse from Group property or default to current balance + issued
                if (decimal.TryParse(asset.Group, out var groupQty) && groupQty > 0)
                {
                    totalEntryQty = groupQty;
                }
                else
                {
                    totalEntryQty = (asset.BalanceQty ?? 100) + (asset.IssuedQty ?? 0);
                }
                asset.EntryQty = totalEntryQty;
            }

            decimal currentIssuedQty = asset.IssuedQty ?? 0;
            decimal currentBalanceQty = asset.BalanceQty ?? (totalEntryQty - currentIssuedQty);

            // Step 2: Input Validation
            if (request.IssueQuantity <= 0)
            {
                return BadRequest(new { message = "Issue quantity must be greater than 0." });
            }
            if (request.IssueQuantity > currentBalanceQty)
            {
                return BadRequest(new { message = $"Cannot issue {request.IssueQuantity}. Available balance is only {currentBalanceQty}." });
            }

            // Step 3: Stock Quantity Updates
            decimal newIssuedQty = currentIssuedQty + request.IssueQuantity;
            decimal newBalanceQty = totalEntryQty - newIssuedQty;
            AssetStatus newStatus = (newBalanceQty == 0) ? AssetStatus.FullyIssued : AssetStatus.Available;

            // Update Asset properties
            asset.EntryQty = totalEntryQty;
            asset.IssuedQty = newIssuedQty;
            asset.BalanceQty = newBalanceQty;
            asset.Group = newBalanceQty.ToString();
            asset.Status = newStatus;
            if (!string.IsNullOrEmpty(request.Unit))
            {
                asset.Unit = request.Unit;
            }

            _unitOfWork.Repository<Asset>().Update(asset);

            // Step 4: Save Issue Audit Record in Database
            var existingIssuances = await _unitOfWork.Repository<AssetIssuance>().GetAllAsync(cancellationToken);
            var count = existingIssuances.Count;
            var issueCode = $"ISS-{(count + 1001)}";

            Guid? userId = null;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            // Fetch Site Name if missing
            string siteName = request.SiteName ?? string.Empty;
            if (string.IsNullOrEmpty(siteName))
            {
                var site = await _unitOfWork.Repository<Site>().GetByIdAsync(request.SiteId, cancellationToken);
                siteName = site?.Name ?? "Main Site";
            }

            var issuanceRecord = new AssetIssuance
            {
                IssueCode = issueCode,
                AssetId = asset.Id,
                AssetNumber = !string.IsNullOrEmpty(request.AssetNumber) ? request.AssetNumber : asset.AssetNumber,
                AssetName = !string.IsNullOrEmpty(request.Name) ? request.Name : asset.Name,
                IssuedToPerson = request.IssuedToPerson,
                Contractor = request.Contractor,
                IssueQuantity = request.IssueQuantity,
                Unit = !string.IsNullOrEmpty(request.Unit) ? request.Unit : (asset.Unit ?? "Pcs"),
                Purpose = request.Purpose,
                SiteId = request.SiteId,
                SiteName = siteName,
                IssuedDate = request.IssuedDate ?? DateTime.UtcNow,
                PreviousIssuedQty = currentIssuedQty,
                NewIssuedQty = newIssuedQty,
                PreviousBalanceQty = currentBalanceQty,
                NewBalanceQty = newBalanceQty,
                IssuedByUserId = userId,
                Remarks = request.Remarks
            };

            await _unitOfWork.Repository<AssetIssuance>().AddAsync(issuanceRecord, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(_mapper.Map<AssetIssuanceDto>(issuanceRecord));
        }
    }
}
