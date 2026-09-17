export interface Envelope<T> {
  isSuccess: boolean
  code: number
  errorMessage: string
  data: T
}

export interface MenuItem {
  id: number
  menu_name: string
  module: string
  vue_path: string
  vue_path_detail: string
  vue_directory: string
  sort: number
  menu_actions: string[]
}

export interface LoginResponseData {
  user_num: string
  user_name: string
  user_id: number
  user_role: string
  userrole_id: number
  tenant_id: number
  expire: number
  access_token: string
  refresh_token: string
}

export interface AuthUser {
  userId: number
  userNum: string
  userName: string
  userRole: string
  userroleId: number
  tenantId: number
}

export interface StoredAuth {
  token: string
  refreshToken: string
  expiresAt: number
  user: AuthUser
  menuList: MenuItem[]
}
