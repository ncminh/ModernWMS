import { type ChangeEvent, type FormEvent, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Eye, EyeOff } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { getUserAuthority, login } from '@/lib/auth-api'
import { useAuth } from '@/lib/auth-context'

const REMEMBERED_USER_KEY = 'wms_remembered_user'

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { signIn } = useAuth()

  const [userName, setUserName] = useState(() => localStorage.getItem(REMEMBERED_USER_KEY) ?? '')
  const [password, setPassword] = useState('')
  const [remember, setRemember] = useState(() => Boolean(localStorage.getItem(REMEMBERED_USER_KEY)))
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const redirectTo = (location.state as { from?: string } | null)?.from ?? '/'

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')

    if (!userName.trim()) {
      setError('Please enter your username')
      return
    }
    if (!password) {
      setError('Please enter your password')
      return
    }

    setSubmitting(true)
    try {
      const loginRes = await login({ userName, password })
      if (!loginRes?.isSuccess) {
        setError(loginRes?.errorMessage || 'Login failed')
        return
      }

      const authorityRes = await getUserAuthority(loginRes.data.userrole_id)
      if (!authorityRes?.isSuccess) {
        setError(authorityRes?.errorMessage || 'Failed to load menu authority')
        return
      }
      if (!authorityRes.data?.length) {
        setError('This account has no menu authority assigned')
        return
      }

      signIn({
        token: loginRes.data.access_token,
        refreshToken: loginRes.data.refresh_token,
        expiresAt: Date.now() + loginRes.data.expire * 60 * 1000,
        user: {
          userId: loginRes.data.user_id,
          userNum: loginRes.data.user_num,
          userName: loginRes.data.user_name,
          userRole: loginRes.data.user_role,
          userroleId: loginRes.data.userrole_id,
          tenantId: loginRes.data.tenant_id,
        },
        menuList: authorityRes.data,
      })

      if (remember) {
        localStorage.setItem(REMEMBERED_USER_KEY, userName)
      } else {
        localStorage.removeItem(REMEMBERED_USER_KEY)
      }

      navigate(redirectTo, { replace: true })
    } catch {
      setError('Unable to reach the server. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-svh w-full items-center justify-center bg-muted/40 p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-xl">Welcome back</CardTitle>
          <CardDescription>Sign in to ModernWMS to continue</CardDescription>
        </CardHeader>
        <CardContent>
          <form className="flex flex-col gap-5" onSubmit={handleSubmit} noValidate>
            <div className="flex flex-col gap-2">
              <Label htmlFor="userName">Username</Label>
              <Input
                id="userName"
                name="userName"
                autoComplete="username"
                value={userName}
                onChange={(event: ChangeEvent<HTMLInputElement>) => setUserName(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="password">Password</Label>
              <div className="relative">
                <Input
                  id="password"
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={password}
                  onChange={(event: ChangeEvent<HTMLInputElement>) => setPassword(event.target.value)}
                  className="pr-9"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((value) => !value)}
                  className="absolute inset-y-0 right-2 flex items-center text-muted-foreground"
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                >
                  {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Checkbox
                id="remember"
                checked={remember}
                onCheckedChange={(checked) => setRemember(checked === true)}
              />
              <Label htmlFor="remember" className="font-normal text-muted-foreground">
                Remember my username
              </Label>
            </div>

            {error ? (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            ) : null}

            <Button type="submit" className="w-full" disabled={submitting}>
              {submitting ? 'Signing in…' : 'Sign in'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
