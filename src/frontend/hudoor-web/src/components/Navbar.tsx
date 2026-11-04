import React from 'react';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { useLanguage } from '../context/LanguageContext';

interface NavbarProps {
  currentView: string;
  onNavigate: (view: string) => void;
  onOpenPasswordChange: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({ currentView, onNavigate, onOpenPasswordChange }) => {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const { language, toggleLanguage, t } = useLanguage();

  return (
    <nav className="navbar">
      <div className="brand-section" onClick={() => onNavigate('dashboard')}>
        <img src="/logo.svg" alt="Hudoor Logo" style={{ width: '38px', height: '38px', borderRadius: '10px', boxShadow: '0 4px 12px rgba(0,0,0,0.15)' }} />
        <div className="brand-text">{t('brandName')}</div>
      </div>

      {user && (
        <ul className="nav-links">
          {user.role === 'Teacher' && (
            <>
              <li>
                <button
                  type="button"
                  className={`nav-item ${currentView === 'dashboard' ? 'active' : ''}`}
                  onClick={() => onNavigate('dashboard')}
                >
                  {t('dashboard')}
                </button>
              </li>
              <li>
                <button
                  type="button"
                  className={`nav-item ${currentView === 'live-session' ? 'active' : ''}`}
                  onClick={() => onNavigate('live-session')}
                >
                  {t('liveSession')}
                </button>
              </li>
              <li>
                <button
                  type="button"
                  className={`nav-item ${currentView === 'fast-roster' ? 'active' : ''}`}
                  onClick={() => onNavigate('fast-roster')}
                >
                  {t('fastRoster')}
                </button>
              </li>
              <li>
                <button
                  type="button"
                  className={`nav-item ${currentView === 'students' ? 'active' : ''}`}
                  onClick={() => onNavigate('students')}
                >
                  {t('students')}
                </button>
              </li>
              <li>
                <button
                  type="button"
                  className={`nav-item ${currentView === 'reports' ? 'active' : ''}`}
                  onClick={() => onNavigate('reports')}
                >
                  {t('reports')}
                </button>
              </li>
              <li>
                <button
                  type="button"
                  className={`nav-item ${currentView === 'rollover' ? 'active' : ''}`}
                  onClick={() => onNavigate('rollover')}
                >
                  {t('rollover')}
                </button>
              </li>
            </>
          )}

          {user.role === 'Student' && (
            <>
              <li>
                <button
                  type="button"
                  className={`nav-item ${currentView === 'dashboard' ? 'active' : ''}`}
                  onClick={() => onNavigate('dashboard')}
                >
                  {t('myAttendance')}
                </button>
              </li>
              <li>
                <button
                  type="button"
                  className={`nav-item ${currentView === 'student-scanner' ? 'active' : ''}`}
                  onClick={() => onNavigate('student-scanner')}
                >
                  {t('scanQrBtn')}
                </button>
              </li>
            </>
          )}

          {user.role === 'PlatformAdmin' && (
            <li>
              <button
                type="button"
                className={`nav-item ${currentView === 'admin' ? 'active' : ''}`}
                onClick={() => onNavigate('admin')}
              >
                {t('dashboard')}
              </button>
            </li>
          )}
        </ul>
      )}

      <div className="nav-controls">
        <button
          type="button"
          className="btn btn-outline"
          onClick={toggleTheme}
          title="Toggle Light / Dark theme"
          style={{ minHeight: '38px', padding: '0.4rem 0.8rem' }}
        >
          {theme === 'light' ? '🌙' : '☀️'}
        </button>

        <button
          type="button"
          className="btn btn-outline"
          onClick={toggleLanguage}
          title="Toggle Language"
          style={{ minHeight: '38px', padding: '0.4rem 0.8rem' }}
        >
          {language === 'ar' ? 'EN' : 'عربي'}
        </button>

        {user && (
          <>
            <button
              type="button"
              className="btn btn-outline"
              onClick={onOpenPasswordChange}
              title={t('changePassword')}
              style={{ minHeight: '38px', padding: '0.4rem 0.8rem' }}
            >
              🔑
            </button>
            <button
              type="button"
              className="btn btn-outline"
              onClick={logout}
              style={{ minHeight: '38px', padding: '0.4rem 0.8rem', color: 'var(--status-absent)' }}
            >
              {t('logout')}
            </button>
          </>
        )}
      </div>
    </nav>
  );
};
