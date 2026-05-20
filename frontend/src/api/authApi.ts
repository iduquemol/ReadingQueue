import { apiClient } from '@/lib/axios'
import type { AuthResponse, LoginPayload, RegisterPayload } from '@/types'

export const authApi = {
  login:    (p: LoginPayload)    => apiClient.post<AuthResponse>('/auth/login',    p),
  register: (p: RegisterPayload) => apiClient.post<AuthResponse>('/auth/register', p),
  refresh:  (token: string)      => apiClient.post<{ accessToken: string; refreshToken: string }>(
                                      '/auth/refresh', { refreshToken: token }),
  logout:   (token: string)      => apiClient.post('/auth/logout', { refreshToken: token }),
  me:       ()                   => apiClient.get<{ id: number; email: string; displayName: string }>('/auth/me'),
}
