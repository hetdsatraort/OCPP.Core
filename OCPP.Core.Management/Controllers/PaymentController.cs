using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Management.Models.Payment;
using System;
using System.Collections.Generic;
using System.Net.Http;
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
        private readonly string _paymentGatewayUrl;

        public PaymentController(
            ILogger<PaymentController> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
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
    }
}

