import md5 from 'md5'
import { http } from '@/lib/http'
import type { Envelope, LoginResponseData, MenuItem } from '@/lib/types'

export interface LoginParams {
  userName: string
  password: string
}

export function login({ userName, password }: LoginParams) {
  return http.post<Envelope<LoginResponseData>>('/login', {
    user_name: userName,
    password: md5(password),
  })
}

export function getUserAuthority(userroleId: number) {
  return http.get<Envelope<MenuItem[]>>('/rolemenu/authority', {
    params: { userrole_id: userroleId },
  })
}
