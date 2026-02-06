using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Management.Models.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly ILogger<PaymentController> _logger;
        private readonly IConfiguration _config;
        private readonly OCPPCoreContext _dbContext;
        private readonly string _paymentGatewayUrl;

        public PaymentController(
            ILogger<PaymentController> logger,
            IConfiguration config,
            OCPPCoreContext dbContext)
        {
            _logger = logger;
            _config = config;
            _dbContext = dbContext;
            _paymentGatewayUrl = _config.GetValue<string>("PaymentGatewayUrl");

            if (string.IsNullOrEmpty(_paymentGatewayUrl))
            {
                _logger.LogWarning("PaymentGatewayUrl is not configured in appsettings.json");
            }
        }

        /// <summary>
        /// Get Razorpay Key ID for frontend integration
        /// </summary>
        [HttpGet("razorpay-key")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRazorpayKey()
        {
            try
            {
                if (string.IsNullOrEmpty(_paymentGatewayUrl))
                {
                    return StatusCode(500, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Payment gateway URL not configured"
                    });
                }

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync($"{_paymentGatewayUrl}/api/razorpay-key");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var razorpayKey = JsonConvert.DeserializeObject<RazorpayKeyResponseDto>(content);

                        return Ok(new PaymentResponseDto
                        {
                            Success = true,
                            Message = "Razorpay key retrieved successfully",
                            Data = razorpayKey
                        });
                    }

                    _logger.LogError($"Failed to get Razorpay key. Status: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Failed to retrieve Razorpay key"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Razorpay key");
                return StatusCode(500, new PaymentResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving Razorpay key"
                });
            }
        }

        /// <summary>
        /// Create a new Razorpay order
        /// </summary>
        [HttpPost("create-order")]
        [Authorize]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequestDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(_paymentGatewayUrl))
                {
                    return StatusCode(500, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Payment gateway URL not configured"
                    });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Invalid amount"
                    });
                }

                using (var httpClient = new HttpClient())
                {
                    var jsonContent = JsonConvert.SerializeObject(new
                    {
                        amount = request.Amount,
                        currency = request.Currency ?? "INR",
                        receipt = request.Receipt ?? $"receipt_{DateTime.UtcNow.Ticks}",
                        notes = request.Notes ?? new Dictionary<string, string>()
                    });

                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync($"{_paymentGatewayUrl}/create-order", content);

                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var order = JsonConvert.DeserializeObject<CreateOrderResponseDto>(responseContent);

                        _logger.LogInformation($"Order created successfully: {order.Id}, Amount: ₹{request.Amount}");

                        return Ok(new PaymentResponseDto
                        {
                            Success = true,
                            Message = "Order created successfully",
                            Data = order
                        });
                    }

                    _logger.LogError($"Failed to create order. Status: {response.StatusCode}, Response: {responseContent}");
                    return StatusCode((int)response.StatusCode, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Failed to create order",
                        Data = responseContent
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, new PaymentResponseDto
                {
                    Success = false,
                    Message = "An error occurred while creating order"
                });
            }
        }

        /// <summary>
        /// Verify payment signature after successful payment
        /// </summary>
        [HttpPost("verify-payment")]
        [Authorize]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequestDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(_paymentGatewayUrl))
                {
                    return StatusCode(500, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Payment gateway URL not configured"
                    });
                }

                if (string.IsNullOrEmpty(request.RazorpayOrderId) ||
                    string.IsNullOrEmpty(request.RazorpayPaymentId) ||
                    string.IsNullOrEmpty(request.RazorpaySignature))
                {
                    return BadRequest(new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Missing required payment verification parameters"
                    });
                }

                using (var httpClient = new HttpClient())
                {
                    var jsonContent = JsonConvert.SerializeObject(new
                    {
                        razorpay_order_id = request.RazorpayOrderId,
                        razorpay_payment_id = request.RazorpayPaymentId,
                        razorpay_signature = request.RazorpaySignature
                    });

                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync($"{_paymentGatewayUrl}/verify-payment", content);

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var verificationResult = JsonConvert.DeserializeObject<VerifyPaymentResponseDto>(responseContent);

                    if (response.IsSuccessStatusCode && verificationResult.Status == "ok")
                    {
                        _logger.LogInformation($"Payment verified successfully: Payment ID: {request.RazorpayPaymentId}, Order ID: {request.RazorpayOrderId}");

                        return Ok(new PaymentResponseDto
                        {
                            Success = true,
                            Message = "Payment verified successfully",
                            Data = verificationResult
                        });
                    }

                    _logger.LogWarning($"Payment verification failed: {responseContent}");
                    return BadRequest(new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Payment verification failed",
                        Data = verificationResult
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment");
                return StatusCode(500, new PaymentResponseDto
                {
                    Success = false,
                    Message = "An error occurred while verifying payment"
                });
            }
        }

        /// <summary>
        /// Get all orders
        /// </summary>
        [HttpGet("orders")]
        [Authorize]
        public async Task<IActionResult> GetAllOrders()
        {
            try
            {
                if (string.IsNullOrEmpty(_paymentGatewayUrl))
                {
                    return StatusCode(500, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Payment gateway URL not configured"
                    });
                }

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync($"{_paymentGatewayUrl}/api/orders");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var orders = JsonConvert.DeserializeObject<List<OrderDto>>(content);

                        return Ok(new PaymentResponseDto
                        {
                            Success = true,
                            Message = "Orders retrieved successfully",
                            Data = orders
                        });
                    }

                    _logger.LogError($"Failed to get orders. Status: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Failed to retrieve orders"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                return StatusCode(500, new PaymentResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving orders"
                });
            }
        }

        /// <summary>
        /// Get a specific order by ID
        /// </summary>
        [HttpGet("orders/{orderId}")]
        [Authorize]
        public async Task<IActionResult> GetOrderById(string orderId)
        {
            try
            {
                if (string.IsNullOrEmpty(_paymentGatewayUrl))
                {
                    return StatusCode(500, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Payment gateway URL not configured"
                    });
                }

                if (string.IsNullOrEmpty(orderId))
                {
                    return BadRequest(new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Order ID is required"
                    });
                }

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync($"{_paymentGatewayUrl}/api/orders/{orderId}");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var order = JsonConvert.DeserializeObject<OrderDto>(content);

                        return Ok(new PaymentResponseDto
                        {
                            Success = true,
                            Message = "Order retrieved successfully",
                            Data = order
                        });
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound(new PaymentResponseDto
                        {
                            Success = false,
                            Message = "Order not found"
                        });
                    }

                    _logger.LogError($"Failed to get order. Status: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Failed to retrieve order"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order");
                return StatusCode(500, new PaymentResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving order"
                });
            }
        }

        /// <summary>
        /// Health check for payment gateway
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                if (string.IsNullOrEmpty(_paymentGatewayUrl))
                {
                    return StatusCode(500, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Payment gateway URL not configured"
                    });
                }

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    var response = await httpClient.GetAsync($"{_paymentGatewayUrl}/health");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var health = JsonConvert.DeserializeObject<HealthCheckResponseDto>(content);

                        return Ok(new PaymentResponseDto
                        {
                            Success = true,
                            Message = "Payment gateway is healthy",
                            Data = health
                        });
                    }

                    return StatusCode((int)response.StatusCode, new PaymentResponseDto
                    {
                        Success = false,
                        Message = "Payment gateway is not responding"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking payment gateway health");
                return StatusCode(500, new PaymentResponseDto
                {
                    Success = false,
                    Message = "Payment gateway is unreachable"
                });
            }
        }

        /// <summary>
        /// Add credits to user wallet with payment validation
        /// Ensures secure payment processing with duplicate prevention
        /// </summary>
        [HttpPost("add-credits")]
        [Authorize]
        public async Task<IActionResult> AddCredits([FromBody] AddCreditsRequestDto request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.UserId) ||
                    string.IsNullOrEmpty(request.OrderId) ||
                    string.IsNullOrEmpty(request.PaymentId) ||
                    string.IsNullOrEmpty(request.PaymentSignature) ||
                    request.Amount <= 0)
                {
                    return BadRequest(new AddCreditsResponseDto
                    {
                        Success = false,
                        Message = "Invalid request. All fields are required and amount must be greater than 0"
                    });
                }

                // Get client IP and User Agent for security
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                // Check for duplicate payment processing
                var existingValidation = await _dbContext.PaymentValidations
                    .FirstOrDefaultAsync(pv => pv.OrderId == request.OrderId && 
                                               (pv.Status == "Verified" || pv.Status == "Processed"));

                if (existingValidation != null)
                {
                    _logger.LogWarning($"Duplicate payment attempt detected for OrderId: {request.OrderId}, UserId: {request.UserId}");
                    return BadRequest(new AddCreditsResponseDto
                    {
                        Success = false,
                        Message = "This payment has already been processed",
                        ValidationId = existingValidation.RecId
                    });
                }

                // Verify user exists
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == request.UserId && u.Active == 1);
                if (user == null)
                {
                    return NotFound(new AddCreditsResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Create initial validation record with Pending status
                var validationId = Guid.NewGuid().ToString();
                var securityHash = GenerateSecurityHash(request.OrderId, request.PaymentId, request.UserId, request.Amount);

                var paymentValidation = new PaymentValidation
                {
                    RecId = validationId,
                    UserId = request.UserId,
                    OrderId = request.OrderId,
                    PaymentId = request.PaymentId,
                    PaymentSignature = request.PaymentSignature,
                    Amount = (long)(request.Amount * 100), // Convert to smallest currency unit
                    Currency = request.Currency ?? "INR",
                    Status = "Pending",
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    VerificationAttempts = 1,
                    SecurityHash = securityHash,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.PaymentValidations.Add(paymentValidation);
                await _dbContext.SaveChangesAsync();

                // Verify payment with Razorpay
                if (string.IsNullOrEmpty(_paymentGatewayUrl))
                {
                    await UpdateValidationStatus(paymentValidation, "Failed", "Payment gateway URL not configured");
                    return StatusCode(500, new AddCreditsResponseDto
                    {
                        Success = false,
                        Message = "Payment gateway URL not configured",
                        ValidationId = validationId
                    });
                }

                using (var httpClient = new HttpClient())
                {
                    var verifyPayload = new
                    {
                        razorpay_order_id = request.OrderId,
                        razorpay_payment_id = request.PaymentId,
                        razorpay_signature = request.PaymentSignature
                    };

                    var jsonContent = JsonConvert.SerializeObject(verifyPayload);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync($"{_paymentGatewayUrl}/verify-payment", content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        await UpdateValidationStatus(paymentValidation, "Failed", $"Payment verification failed: {responseContent}");
                        _logger.LogError($"Payment verification failed for OrderId: {request.OrderId}, Response: {responseContent}");
                        
                        return BadRequest(new AddCreditsResponseDto
                        {
                            Success = false,
                            Message = "Payment verification failed. Please contact support if amount was deducted.",
                            ValidationId = validationId
                        });
                    }

                    var verificationResult = JsonConvert.DeserializeObject<VerifyPaymentResponseDto>(responseContent);
                    if (verificationResult.Status != "ok")
                    {
                        await UpdateValidationStatus(paymentValidation, "Failed", $"Verification status not OK: {verificationResult.Message}");
                        _logger.LogError($"Payment verification status not OK for OrderId: {request.OrderId}");
                        
                        return BadRequest(new AddCreditsResponseDto
                        {
                            Success = false,
                            Message = "Payment verification failed",
                            ValidationId = validationId
                        });
                    }
                }

                // Payment verified successfully, update validation status
                paymentValidation.Status = "Verified";
                paymentValidation.VerifiedAt = DateTime.UtcNow;
                paymentValidation.VerificationMessage = "Payment verified successfully";
                paymentValidation.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Get current wallet balance
                var lastTransaction = await _dbContext.WalletTransactionLogs
                    .Where(w => w.UserId == request.UserId)
                    .OrderByDescending(w => w.CreatedOn)
                    .FirstOrDefaultAsync();

                decimal previousBalance = 0;
                if (lastTransaction != null && decimal.TryParse(lastTransaction.CurrentCreditBalance, out var lastBalance))
                {
                    previousBalance = lastBalance;
                }

                decimal newBalance = previousBalance + request.Amount;

                // Create payment history record
                var paymentHistoryId = Guid.NewGuid().ToString();
                var paymentHistory = new PaymentHistory
                {
                    RecId = paymentHistoryId,
                    TransactionType = "Credit_Add",
                    UserId = request.UserId,
                    SessionDuration = TimeSpan.Zero,
                    PaymentMethod = "Razorpay",
                    OrderId = request.OrderId,
                    PaymentId = request.PaymentId,
                    AdditionalInfo1 = $"Amount: {request.Amount}",
                    AdditionalInfo2 = $"IP: {ipAddress}",
                    AdditionalInfo3 = $"ValidationId: {validationId}",
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.PaymentHistories.Add(paymentHistory);

                // Create wallet transaction log
                var walletTransactionId = Guid.NewGuid().ToString();
                var walletLog = new WalletTransactionLog
                {
                    RecId = walletTransactionId,
                    UserId = request.UserId,
                    PreviousCreditBalance = previousBalance.ToString("F2"),
                    CurrentCreditBalance = newBalance.ToString("F2"),
                    TransactionType = "Credit",
                    PaymentRecId = paymentHistoryId,
                    AdditionalInfo1 = $"OrderId: {request.OrderId}",
                    AdditionalInfo2 = $"PaymentId: {request.PaymentId}",
                    AdditionalInfo3 = $"ValidationId: {validationId}",
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.WalletTransactionLogs.Add(walletLog);

                // Update payment validation with references
                paymentValidation.Status = "Processed";
                paymentValidation.ProcessedAt = DateTime.UtcNow;
                paymentValidation.PaymentHistoryId = paymentHistoryId;
                paymentValidation.WalletTransactionId = walletTransactionId;
                paymentValidation.UpdatedOn = DateTime.UtcNow;

                // Update user credit balance
                user.CreditBalance = newBalance.ToString("F2");
                user.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Credits added successfully - UserId: {request.UserId}, Amount: {request.Amount}, OrderId: {request.OrderId}, NewBalance: {newBalance}");

                return Ok(new AddCreditsResponseDto
                {
                    Success = true,
                    Message = "Credits added successfully",
                    NewBalance = newBalance,
                    ValidationId = validationId,
                    TransactionId = walletTransactionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding credits for UserId: {request?.UserId}, OrderId: {request?.OrderId}");
                return StatusCode(500, new AddCreditsResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding credits. Please contact support."
                });
            }
        }

        /// <summary>
        /// Update payment validation status
        /// </summary>
        private async Task UpdateValidationStatus(PaymentValidation validation, string status, string message)
        {
            validation.Status = status;
            validation.VerificationMessage = message;
            if (status == "Failed")
            {
                validation.FailureReason = message;
            }
            validation.UpdatedOn = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Generate security hash for payment validation
        /// </summary>
        private string GenerateSecurityHash(string orderId, string paymentId, string userId, decimal amount)
        {
            var data = $"{orderId}|{paymentId}|{userId}|{amount}|{DateTime.UtcNow:yyyyMMdd}";
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}

