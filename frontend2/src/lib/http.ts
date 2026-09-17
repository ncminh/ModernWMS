import axios, { type AxiosRequestConfig } from 'axios'
import { readAuth } from '@/lib/auth-storage'

const baseURL = `${import.meta.env.VITE_BASE_PATH}:${import.meta.env.VITE_SERVER_PORT}`

const NO_AUTH_URLS = ['/login', '/user/register']

const instance = axios.create({
  baseURL,
  timeout: 10000,
})

instance.interceptors.request.use((config) => {
  const token = readAuth()?.token
  if (token && !NO_AUTH_URLS.includes(config.url ?? '')) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

instance.interceptors.response.use(
  (response) => response.data,
  (error) => Promise.reject(error),
)

// The response interceptor above unwraps `AxiosResponse` down to just
// `response.data`, so callers get the envelope directly — this narrower
// type reflects that actual runtime shape instead of the raw axios types.
export const http = instance as unknown as {
  get<T>(url: string, config?: AxiosRequestConfig): Promise<T>
  post<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T>
}
