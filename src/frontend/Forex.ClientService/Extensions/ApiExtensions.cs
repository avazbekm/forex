namespace Forex.ClientService.Extensions;

using Forex.ClientService.Models.Commons;
using Refit;
using System.Text.Json;

public static class ApiExtensions
{
    public static async Task<Response<T>> Handle<T>(
        this Task<Response<T>> task,
        Action<bool>? setLoading = null)
    {
        try
        {
            setLoading?.Invoke(true);
            return await task;
        }
        catch (ApiException apiEx)
        {
            try
            {
                using var doc = JsonDocument.Parse(apiEx.Content ?? "{}");
                var root = doc.RootElement;

                var statusCode = root.TryGetProperty("statusCode", out var statusProp)
                    ? statusProp.GetInt32()
                    : (int)apiEx.StatusCode;

                var message = root.TryGetProperty("message", out var messageProp)
                    ? messageProp.GetString() ?? apiEx.Message
                    : apiEx.Message;

                return new Response<T>
                {
                    StatusCode = statusCode,
                    Message = message
                };
            }
            catch { }

            return new Response<T>
            {
                StatusCode = (int)apiEx.StatusCode,
                Message = apiEx.ReasonPhrase ?? apiEx.Message
            };
        }
        catch (Exception ex)
        {
            return new Response<T>
            {
                StatusCode = 500,
                Message = ex.Message
            };
        }
        finally
        {
            setLoading?.Invoke(false);
        }
    }

    public static async Task<Response<T>> Handle<T>(
        this Task<IApiResponse<Response<T>>> task,
        Action<bool>? setLoading = null) where T : class
    {
        try
        {
            setLoading?.Invoke(true);
            var apiResponse = await task;
            return apiResponse.Content ?? new Response<T>
            {
                StatusCode = (int)apiResponse.StatusCode,
                Message = apiResponse.Error?.Message ?? "No content"
            };
        }
        catch (ApiException apiEx)
        {
            try
            {
                using var doc = JsonDocument.Parse(apiEx.Content ?? "{}");
                var root = doc.RootElement;

                var statusCode = root.TryGetProperty("statusCode", out var statusProp)
                    ? statusProp.GetInt32()
                    : (int)apiEx.StatusCode;

                var message = root.TryGetProperty("message", out var messageProp)
                    ? messageProp.GetString() ?? apiEx.Message
                    : apiEx.Message;

                return new Response<T>
                {
                    StatusCode = statusCode,
                    Message = message
                };
            }
            catch { }

            return new Response<T>
            {
                StatusCode = (int)apiEx.StatusCode,
                Message = apiEx.ReasonPhrase ?? apiEx.Message
            };
        }
        catch (Exception ex)
        {
            return new Response<T>
            {
                StatusCode = 500,
                Message = ex.Message
            };
        }
        finally
        {
            setLoading?.Invoke(false);
        }
    }

    public static async Task<PagedResponse<T>> HandleWithPagination<T>(
        this Task<IApiResponse<Response<T>>> task,
        Action<bool>? setLoading = null) where T : class
    {
        try
        {
            setLoading?.Invoke(true);
            var apiResponse = await task;
            
            PagedListMetadata? metadata = null;
            
            // Try different header variations
            var headerKeys = new[] { "X-Pagination", "x-pagination", "Pagination", "pagination" };
            string? paginationJson = null;

            foreach (var key in headerKeys)
            {
                if (apiResponse.Headers.TryGetValues(key, out var values))
                {
                    paginationJson = values.FirstOrDefault();
                    if (!string.IsNullOrEmpty(paginationJson))
                        break;
                }
            }
            
            if (!string.IsNullOrEmpty(paginationJson))
            {
                try
                {
                    metadata = JsonSerializer.Deserialize<PagedListMetadata>(paginationJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { }
            }

            var response = apiResponse.Content;
            return new PagedResponse<T>
            {
                Data = response?.Data ?? default!,
                Message = response?.Message ?? string.Empty,
                StatusCode = response?.StatusCode ?? (int)apiResponse.StatusCode,
                Metadata = metadata
            };
        }
        catch (ApiException apiEx)
        {
            try
            {
                using var doc = JsonDocument.Parse(apiEx.Content ?? "{}");
                var root = doc.RootElement;

                var statusCode = root.TryGetProperty("statusCode", out var statusProp)
                    ? statusProp.GetInt32()
                    : (int)apiEx.StatusCode;

                var message = root.TryGetProperty("message", out var messageProp)
                    ? messageProp.GetString() ?? apiEx.Message
                    : apiEx.Message;

                return new PagedResponse<T>
                {
                    StatusCode = statusCode,
                    Message = message
                };
            }
            catch { }

            return new PagedResponse<T>
            {
                StatusCode = (int)apiEx.StatusCode,
                Message = apiEx.ReasonPhrase ?? apiEx.Message
            };
        }
        catch (Exception ex)
        {
            return new PagedResponse<T>
            {
                StatusCode = 500,
                Message = ex.Message
            };
        }
        finally
        {
            setLoading?.Invoke(false);
        }
    }
}