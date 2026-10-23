// Flutter HTTP starter. Add package:http to pubspec.yaml and inject your own
// platform secure storage adapter (flutter_secure_storage is a common option).
// Do NOT hardcode tokens, passwords or a localhost production URL.
import 'dart:convert';
import 'package:http/http.dart' as http;

class DareCareApiException implements Exception {
  final int status;
  final String message;
  DareCareApiException(this.status, this.message);
  @override
  String toString() => 'HTTP $status: $message';
}

abstract class SecureSessionStore {
  Future<String?> accessToken();
  Future<String?> refreshToken();
  Future<void> save(String accessToken, String refreshToken);
  Future<void> clear();
}

class DareCareApi {
  final String baseUrl;
  final SecureSessionStore store;
  final http.Client httpClient;

  DareCareApi({required this.baseUrl, required this.store, http.Client? client})
      : httpClient = client ?? http.Client() {
    if (!baseUrl.startsWith('https://') &&
        !baseUrl.startsWith('http://127.0.0.1:') &&
        !baseUrl.startsWith('http://localhost:') &&
        !baseUrl.startsWith('http://10.0.2.2:')) {
      throw ArgumentError('Use HTTPS for remote DareCare APIs.');
    }
  }

  Future<Map<String, dynamic>> login(String email, String password) async {
    final response = await httpClient.post(Uri.parse('$baseUrl/api/v1/auth/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'email': email, 'password': password}));
    final json = _checked(response);
    await store.save(json['accessToken'] as String, json['refreshToken'] as String);
    return json;
  }

  Future<bool> _refresh() async {
    final token = await store.refreshToken();
    if (token == null) return false;
    final response = await httpClient.post(Uri.parse('$baseUrl/api/v1/auth/refresh'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'refreshToken': token}));
    if (response.statusCode != 200) {
      await store.clear();
      return false;
    }
    final data = _checked(response);
    await store.save(data['accessToken'] as String, data['refreshToken'] as String);
    return true;
  }

  Future<Map<String, dynamic>> request(String path,
      {String method = 'GET', Object? body}) async {
    Future<http.Response> send() async {
      final token = await store.accessToken();
      return httpClient.send(http.Request(method, Uri.parse('$baseUrl$path'))
            ..headers.addAll({
              'Accept': 'application/json',
              if (body != null) 'Content-Type': 'application/json',
              if (token != null) 'Authorization': 'Bearer $token',
            })
            ..body = body != null ? jsonEncode(body) : '')
          .then(http.Response.fromStream);
    }
    var response = await send();
    if (response.statusCode == 401 && await _refresh()) {
      response = await send(); // retry once only
    }
    return _checked(response);
  }

  Map<String, dynamic> _checked(http.Response response) {
    final decoded = response.body.isEmpty ? <String, dynamic>{} : jsonDecode(response.body);
    final data = decoded is Map<String, dynamic> ? decoded : <String, dynamic>{'data': decoded};
    if (response.statusCode >= 200 && response.statusCode < 300) return data;
    final errors = data['errors'];
    final error = errors is Map
        ? errors.values.map((value) => value is List ? value.join(', ') : '$value').join('; ')
        : '${data['detail'] ?? data['message'] ?? data['title'] ?? 'Request failed'}';
    throw DareCareApiException(response.statusCode, error);
  }

  void dispose() => httpClient.close();
}
