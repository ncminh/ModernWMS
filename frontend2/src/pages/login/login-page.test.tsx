import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider, useAuth } from '@/lib/auth-context'
import { getUserAuthority, login } from '@/lib/auth-api'
import { LoginPage } from './login-page'

vi.mock('@/lib/auth-api', () => ({
  login: vi.fn(),
  getUserAuthority: vi.fn(),
}))

const mockedLogin = vi.mocked(login)
const mockedGetUserAuthority = vi.mocked(getUserAuthority)

function AuthProbe() {
  const { isAuthenticated, user } = useAuth()
  return (
    <div data-testid="auth-state">
      {isAuthenticated ? `signed-in:${user?.userName}` : 'signed-out'}
    </div>
  )
}

function renderLoginPage() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <LoginPage />
        <AuthProbe />
      </AuthProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  localStorage.clear()
  vi.clearAllMocks()
})

describe('LoginPage', () => {
  it('shows a validation error instead of calling the API when submitted empty', async () => {
    renderLoginPage()

    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/enter your username/i)
    expect(mockedLogin).not.toHaveBeenCalled()
  })

  it('surfaces the server error message when credentials are rejected', async () => {
    mockedLogin.mockResolvedValue({
      isSuccess: false,
      code: 400,
      errorMessage: 'Invalid credentials',
      data: {
        user_num: '',
        user_name: '',
        user_id: 0,
        user_role: '',
        userrole_id: 0,
        tenant_id: 0,
        expire: 0,
        access_token: '',
        refresh_token: '',
      },
    })

    renderLoginPage()
    await userEvent.type(screen.getByLabelText(/^username$/i), 'admin')
    await userEvent.type(screen.getByLabelText(/^password$/i), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/invalid credentials/i)
    expect(mockedGetUserAuthority).not.toHaveBeenCalled()
  })

  it('blocks sign-in when the account has no granted menus', async () => {
    mockedLogin.mockResolvedValue({
      isSuccess: true,
      code: 200,
      errorMessage: '',
      data: {
        user_num: 'noauth',
        user_name: 'noauth',
        user_id: 2,
        user_role: 'None',
        userrole_id: 2,
        tenant_id: 1,
        expire: 60,
        access_token: 't',
        refresh_token: 'r',
      },
    })
    mockedGetUserAuthority.mockResolvedValue({ isSuccess: true, code: 200, errorMessage: '', data: [] })

    renderLoginPage()
    await userEvent.type(screen.getByLabelText(/^username$/i), 'noauth')
    await userEvent.type(screen.getByLabelText(/^password$/i), '1')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/no menu authority/i)
    expect(screen.getByTestId('auth-state')).toHaveTextContent('signed-out')
  })

  it('signs the user in and stores the granted menus on success', async () => {
    mockedLogin.mockResolvedValue({
      isSuccess: true,
      code: 200,
      errorMessage: '',
      data: {
        user_num: 'admin',
        user_name: 'admin',
        user_id: 1,
        user_role: 'Administrator',
        userrole_id: 1,
        tenant_id: 1,
        expire: 120,
        access_token: 'access-token',
        refresh_token: 'refresh-token',
      },
    })
    mockedGetUserAuthority.mockResolvedValue({
      isSuccess: true,
      code: 200,
      errorMessage: '',
      data: [
        {
          id: 1,
          menu_name: 'Dashboard',
          module: 'base',
          vue_path: 'dashboard',
          vue_path_detail: '',
          vue_directory: 'base/dashboard',
          sort: 0,
          menu_actions: [],
        },
      ],
    })

    renderLoginPage()
    await userEvent.type(screen.getByLabelText(/^username$/i), 'admin')
    await userEvent.type(screen.getByLabelText(/^password$/i), '1')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    await waitFor(() =>
      expect(screen.getByTestId('auth-state')).toHaveTextContent('signed-in:admin'),
    )
    expect(mockedGetUserAuthority).toHaveBeenCalledWith(1)
  })
})
