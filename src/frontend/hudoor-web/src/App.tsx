import React, { useState } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
import { LanguageProvider } from './context/LanguageContext';
import { ThemeProvider } from './context/ThemeContext';
import { Navbar } from './components/Navbar';
import { LoginView } from './components/LoginView';
import { PasswordChangeModal } from './components/PasswordChangeModal';
import { StudentDashboard } from './views/student/StudentDashboard';
import { TeacherDashboard } from './views/teacher/TeacherDashboard';
import { LiveSessionView } from './views/teacher/LiveSessionView';
import { FastTapRosterView } from './views/teacher/FastTapRosterView';
import { StudentsManagementView } from './views/teacher/StudentsManagementView';
import { ReportsView } from './views/teacher/ReportsView';
import { RolloverWizardView } from './views/teacher/RolloverWizardView';
import { AdminDashboard } from './views/admin/AdminDashboard';
import './index.css';

const AppContent: React.FC = () => {
  const { user, isAuthenticated } = useAuth();
  const [currentView, setCurrentView] = useState<string>('dashboard');
  const [contextId, setContextId] = useState<string | undefined>(undefined);
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);

  const handleNavigate = (view: string, id?: string) => {
    setCurrentView(view);
    setContextId(id);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  if (!isAuthenticated || !user) {
    return <LoginView />;
  }

  return (
    <div className="app-container">
      <Navbar
        currentView={currentView}
        onNavigate={handleNavigate}
        onOpenPasswordChange={() => setIsPasswordModalOpen(true)}
      />

      <main className="main-content">
        {/* Mandatory Password Change enforcement (Rule BR-PWD-048) */}
        {user.mustChangePassword && (
          <PasswordChangeModal
            isOpen={true}
            onClose={() => {}}
            isMandatory={true}
          />
        )}

        {/* Voluntary Password Change modal */}
        {isPasswordModalOpen && !user.mustChangePassword && (
          <PasswordChangeModal
            isOpen={isPasswordModalOpen}
            onClose={() => setIsPasswordModalOpen(false)}
            isMandatory={false}
          />
        )}

        {/* Role-Based View Router */}
        {user.role === 'Student' && (
          <StudentDashboard />
        )}

        {user.role === 'PlatformAdmin' && (
          <AdminDashboard />
        )}

        {user.role === 'Teacher' && (
          <>
            {currentView === 'dashboard' && (
              <TeacherDashboard onNavigate={handleNavigate} />
            )}
            {currentView === 'live-session' && (
              <LiveSessionView initialSessionId={contextId} onNavigate={handleNavigate} />
            )}
            {currentView === 'fast-roster' && (
              <FastTapRosterView initialSessionId={contextId} onNavigate={handleNavigate} />
            )}
            {currentView === 'students' && (
              <StudentsManagementView />
            )}
            {currentView === 'reports' && (
              <ReportsView />
            )}
            {currentView === 'rollover' && (
              <RolloverWizardView onNavigate={handleNavigate} />
            )}
          </>
        )}
      </main>
    </div>
  );
};

export default function App() {
  return (
    <ThemeProvider>
      <LanguageProvider>
        <AuthProvider>
          <AppContent />
        </AuthProvider>
      </LanguageProvider>
    </ThemeProvider>
  );
}
