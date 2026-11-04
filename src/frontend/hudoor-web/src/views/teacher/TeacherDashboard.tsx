import React, { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { api } from '../../services/apiClient';
import type { AttendanceSession, TeacherAnalyticsSummary } from '../../types';

interface TeacherDashboardProps {
  onNavigate: (view: string, contextId?: string) => void;
}

export const TeacherDashboard: React.FC<TeacherDashboardProps> = ({ onNavigate }) => {
  const { user } = useAuth();
  const { t } = useLanguage();

  const [analytics, setAnalytics] = useState<TeacherAnalyticsSummary | null>(null);
  const [activeSessions, setActiveSessions] = useState<AttendanceSession[]>([]);
  const [loading, setLoading] = useState(true);

  const loadData = async () => {
    try {
      const [analyticsRes, sessionsRes] = await Promise.all([
        api.get<TeacherAnalyticsSummary>('/teacher/reports/analytics'),
        api.get<AttendanceSession[]>('/teacher/sessions?status=Active'),
      ]);
      setAnalytics(analyticsRes);
      setActiveSessions(sessionsRes);
    } catch (err) {
      console.error('Failed to load dashboard data', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  if (loading) {
    return (
      <div className="card" style={{ padding: '3rem', textAlign: 'center' }}>
        <p>جاري تحميل لوحة التحكم ومؤشرات الأداء...</p>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '2rem' }}>
      {/* Top Banner & Quick Actions */}
      <div
        className="card"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '1rem',
          background: 'linear-gradient(135deg, var(--bg-surface), var(--bg-secondary))',
        }}
      >
        <div>
          <h2 style={{ fontSize: '1.5rem', marginBottom: '0.25rem' }}>
            {t('welcome')}، {user?.fullName}
          </h2>
          <p style={{ fontSize: '0.95rem' }}>
            مساحة عملك السحابية جاهزة ومحدثة بالكامل
          </p>
        </div>

        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => onNavigate('live-session')}
          >
            <span>📷</span> {t('startLiveSessionBtn')}
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => onNavigate('fast-roster')}
          >
            <span>⚡</span> {t('fastRoster')}
          </button>
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => onNavigate('students')}
          >
            <span>➕</span> {t('addStudentBtn')}
          </button>
        </div>
      </div>

      {/* KPI Metrics Grid */}
      {analytics && (
        <div className="metrics-grid">
          <div className="metric-card">
            <span className="metric-label">{t('totalStudents')}</span>
            <span className="metric-value">{analytics.totalStudents}</span>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
              ({analytics.activeStudents} طالب نشط)
            </span>
          </div>

          <div className="metric-card">
            <span className="metric-label">{t('activeGroups')}</span>
            <span className="metric-value">{analytics.totalGroups}</span>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
              مجموعة تعليمية
            </span>
          </div>

          <div className="metric-card">
            <span className="metric-label">{t('totalSessions')}</span>
            <span className="metric-value">{analytics.totalSessions}</span>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
              جلسة حضور منجزة
            </span>
          </div>

          <div className="metric-card">
            <span className="metric-label">{t('attendanceRate')}</span>
            <span className="metric-value" style={{ color: analytics.overallAttendanceRate >= 80 ? 'var(--status-present)' : 'var(--status-late)' }}>
              {analytics.overallAttendanceRate}%
            </span>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
              نسبة الالتزام العامة
            </span>
          </div>

          <div
            className="metric-card"
            style={{ cursor: 'pointer', border: analytics.atRiskStudentsCount > 0 ? '1px solid var(--status-absent)' : undefined }}
            onClick={() => onNavigate('reports')}
          >
            <span className="metric-label" style={{ color: 'var(--status-absent)' }}>{t('atRiskStudents')}</span>
            <span className="metric-value" style={{ color: 'var(--status-absent)' }}>
              {analytics.atRiskStudentsCount}
            </span>
            <span style={{ fontSize: '0.8rem', color: 'var(--status-absent)' }}>
              بحاجة لمتابعة الغياب ⚠️
            </span>
          </div>
        </div>
      )}

      {/* Active Live Sessions Banner */}
      {activeSessions.length > 0 && (
        <div className="card" style={{ border: '2px solid var(--primary-accent)' }}>
          <h3 style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <span style={{ display: 'inline-block', width: '12px', height: '12px', borderRadius: '50%', backgroundColor: 'var(--status-present)', animation: 'pulse 1.5s infinite' }}></span>
            جلسات الحضور النشطة حاليًا (Live)
          </h3>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            {activeSessions.map(session => (
              <div
                key={session.id}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '1rem',
                  borderRadius: 'var(--border-radius-md)',
                  backgroundColor: 'var(--bg-secondary)',
                  flexWrap: 'wrap',
                  gap: '0.75rem',
                }}
              >
                <div>
                  <h4 style={{ fontSize: '1.1rem' }}>{session.title}</h4>
                  <p style={{ fontSize: '0.88rem' }}>
                    {session.subjectName} — {session.groupName} | مسجل حضور: {session.presentCount} من {session.enrolledStudentsCount}
                  </p>
                </div>

                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => onNavigate('live-session', session.id)}
                  >
                    عرض الـ QR على الشاشة
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => onNavigate('fast-roster', session.id)}
                  >
                    التحضير السريع
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Group Attendance Summaries */}
      {analytics && analytics.groupSummaries.length > 0 && (
        <div className="card">
          <h3 style={{ marginBottom: '1.25rem' }}>أداء المجموعات التعليمية</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'start' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--border-color)', color: 'var(--text-secondary)' }}>
                  <th style={{ padding: '0.75rem', textAlign: 'start' }}>المجموعة</th>
                  <th style={{ padding: '0.75rem', textAlign: 'start' }}>المادة</th>
                  <th style={{ padding: '0.75rem', textAlign: 'center' }}>الطلاب</th>
                  <th style={{ padding: '0.75rem', textAlign: 'center' }}>الجلسات</th>
                  <th style={{ padding: '0.75rem', textAlign: 'center' }}>نسبة الحضور</th>
                </tr>
              </thead>
              <tbody>
                {analytics.groupSummaries.map(group => (
                  <tr key={group.groupId} style={{ borderBottom: '1px solid var(--border-color-subtle)' }}>
                    <td style={{ padding: '0.85rem', fontWeight: 600 }}>{group.groupName}</td>
                    <td style={{ padding: '0.85rem' }}>{group.subjectName}</td>
                    <td style={{ padding: '0.85rem', textAlign: 'center' }}>{group.enrolledStudentsCount}</td>
                    <td style={{ padding: '0.85rem', textAlign: 'center' }}>{group.totalSessionsCount}</td>
                    <td style={{ padding: '0.85rem', textAlign: 'center' }}>
                      <span
                        className="badge"
                        style={{
                          backgroundColor: group.attendanceRate >= 80 ? 'var(--status-present-subtle)' : 'var(--status-late-subtle)',
                          color: group.attendanceRate >= 80 ? 'var(--status-present)' : 'var(--status-late)',
                          fontSize: '0.85rem',
                        }}
                      >
                        {group.attendanceRate}%
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};
