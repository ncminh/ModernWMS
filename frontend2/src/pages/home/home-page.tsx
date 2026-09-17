import { ClipboardList, LayoutDashboard, LogOut, Package, Settings, type LucideIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/lib/auth-context'

const PLACEHOLDER_MENUS: { name: string; icon: LucideIcon }[] = [
  { name: 'Dashboard', icon: LayoutDashboard },
  { name: 'Inventory', icon: Package },
  { name: 'Orders', icon: ClipboardList },
  { name: 'Settings', icon: Settings },
]

export function HomePage() {
  const { user, menuList, signOut } = useAuth()

  return (
    <div className="flex min-h-svh w-full">
      <aside className="flex w-64 shrink-0 flex-col border-r bg-card">
        <div className="border-b px-4 py-4">
          <p className="text-lg font-semibold">ModernWMS</p>
          <p className="text-xs text-muted-foreground">React pilot</p>
        </div>
        <nav className="flex flex-1 flex-col gap-1 p-2">
          {PLACEHOLDER_MENUS.map(({ name, icon: Icon }, index) => (
            <button
              key={name}
              type="button"
              className={
                index === 0
                  ? 'flex items-center gap-2 rounded-md bg-accent px-3 py-2 text-sm text-accent-foreground'
                  : 'flex items-center gap-2 rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground'
              }
            >
              <Icon className="size-4" />
              {name}
            </button>
          ))}
        </nav>
        <div className="border-t p-2">
          <Button variant="ghost" className="w-full justify-start gap-2" onClick={signOut}>
            <LogOut className="size-4" />
            Sign out
          </Button>
        </div>
      </aside>

      <main className="flex-1 p-8">
        <h1 className="text-2xl font-semibold">Welcome{user?.userName ? `, ${user.userName}` : ''}</h1>
        <p className="mt-1 text-muted-foreground">
          This is a placeholder home screen for the React migration pilot. Real pages will replace these menu
          placeholders as each module is ported from Vue.
        </p>

        <div className="mt-6 rounded-lg border bg-card p-4">
          <h2 className="text-sm font-medium">Granted menus ({menuList.length})</h2>
          {menuList.length === 0 ? (
            <p className="mt-2 text-sm text-muted-foreground">No menu authority data.</p>
          ) : (
            <ul className="mt-2 space-y-1 text-sm text-muted-foreground">
              {menuList.map((menu) => (
                <li key={menu.id}>
                  {menu.menu_name} <span className="text-xs">({menu.module})</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </main>
    </div>
  )
}
