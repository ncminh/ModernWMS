import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { AuthProvider } from '@/lib/auth-context'
import { writeAuth } from '@/lib/auth-storage'
import { ProtectedRoute } from './protected-route'

function renderAt(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<div>Login screen</div>} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <div>Protected home</div>
              </ProtectedRoute>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  localStorage.clear()
})

describe('ProtectedRoute', () => {
  it('redirects to /login when there is no signed-in user', () => {
    renderAt('/')

    expect(screen.getByText('Login screen')).toBeInTheDocument()
    expect(screen.queryByText('Protected home')).not.toBeInTheDocument()
  })

  it('renders the protected content when a user is signed in', () => {
    writeAuth({
      token: 'token',
      refreshToken: 'refresh',
      expiresAt: Date.now() + 60_000,
      user: {
        userId: 1,
        userNum: 'admin',
        userName: 'admin',
        userRole: 'Administrator',
        userroleId: 1,
        tenantId: 1,
      },
      menuList: [],
    })

    renderAt('/')

    expect(screen.getByText('Protected home')).toBeInTheDocument()
  })
})
