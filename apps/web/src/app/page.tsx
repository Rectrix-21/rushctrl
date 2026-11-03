import { Sidebar } from "@/components/dashboard/sidebar";
import { Dashboard } from "@/components/dashboard/dashboard";

export default function Home() {
  return (
    <>
      <a className="skip-link" href="#main">
        Skip to dashboard
      </a>
      <div className="app">
        <Sidebar />
        <main className="main" id="main">
          <Dashboard />
        </main>
      </div>
    </>
  );
}
