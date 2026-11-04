import React, { useEffect, useState } from 'react';
import { useLanguage } from '../../context/LanguageContext';
import { api } from '../../services/apiClient';
import type { AttendanceSession, RosterStudent } from '../../types';

interface FastTapRosterViewProps {
  initialSessionId?: string;
  onNavigate: (view: string) => void;
}

export const FastTapRosterView: React.FC<FastTapRosterViewProps> = ({ initialSessionId, onNavigate }) => {
  const { t } = useLanguage();

  const [sessions, setSessions] = useState<AttendanceSession[]>([]);
  const [selectedSessionId, setSelectedSessionId] = useState<string>(initialSessionId || '');
  const [roster, setRoster] = useState<RosterStudent[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saveFeedback, setSaveFeedback] = useState<string | null>(null);

  useEffect(() => {
    api.get<AttendanceSession[]>('/teacher/sessions')
      .then(res => {
        setSessions(res);
        if (!selectedSessionId && res.length > 0) {
          // Default to most recent or active session
          const active = res.find(s => s.status === 'Active') || res[0];
          setSelectedSessionId(active.id);
        }
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!selectedSessionId) return;

    setLoading(true);
    api.get<RosterStudent[]>(`/teacher/sessions/${selectedSessionId}/roster`)
      .then(setRoster)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [selectedSessionId]);

  // Fast optimistic update for an individual student
  const handleStatusChange = async (studentId: string, status: 'Present' | 'Late' | 'Excused' | 'Absent') => {
    // 1. Optimistic local update
    setRoster(prev =>
      prev.map(s => (s.studentId === studentId ? { ...s, status } : s))
    );

    // 2. Sync to backend
    try {
      await api.post(`/teacher/sessions/${selectedSessionId}/attendance`, {
        studentId,
        status,
        minutesLate: status === 'Late' ? 10 : undefined,
      });
      showSaveFeedback('تم الحفظ تلقائيًا');
    } catch (err) {
      console.error('Failed to update attendance', err);
    }
  };

  // 1-Touch Mark All Present
  const handleMarkAllPresent = async () => {
    if (!selectedSessionId || roster.length === 0) return;

    setSaving(true);
    // Optimistic update
    setRoster(prev => prev.map(s => ({ ...s, status: 'Present' })));

    try {
      const records = roster.map(s => ({
        studentId: s.studentId,
        status: 'Present',
      }));

      await api.post(`/teacher/sessions/${selectedSessionId}/attendance/bulk`, {
        records,
        markRemainingAsAbsent: false,
      });

      showSaveFeedback('تم تحضير جميع الطلاب كـ حاضر');
    } catch (err) {
      console.error('Failed to bulk mark attendance', err);
    } finally {
      setSaving(false);
    }
  };

  const showSaveFeedback = (msg: string) => {
    setSaveFeedback(msg);
    setTimeout(() => {
      setSaveFeedback(null);
    }, 2500);
  };

  const selectedSession = sessions.find(s => s.id === selectedSessionId);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Header & Controls Bar */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', flexWrap: 'wrap' }}>
          <label className="form-label" style={{ margin: 0 }}>اختر الجلسة:</label>
          <select
            className="form-select"
            value={selectedSessionId}
            onChange={e => setSelectedSessionId(e.target.value)}
            style={{ minWidth: '260px' }}
          >
            {sessions.map(s => (
              <option key={s.id} value={s.id}>
                {s.title} ({s.groupName}) - {s.status}
              </option>
            ))}
          </select>

          {saveFeedback && (
            <span style={{ fontSize: '0.9rem', color: 'var(--status-present)', fontWeight: 700 }}>
              ✓ {saveFeedback}
            </span>
          )}
        </div>

        <div style={{ display: 'flex', gap: '0.75rem' }}>
          <button
            type="button"
            className="btn btn-primary"
            onClick={handleMarkAllPresent}
            disabled={saving || roster.length === 0}
            style={{ minHeight: '48px' }}
          >
            <span>✓✓</span> {t('markAllPresent')}
          </button>
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => onNavigate('dashboard')}
          >
            العودة للرئيسية
          </button>
        </div>
      </div>

      {loading ? (
        <p>جاري تحميل كشف الطلاب...</p>
      ) : roster.length === 0 ? (
        <div className="card" style={{ textAlign: 'center', padding: '3rem' }}>
          <p>لا يوجد طلاب مقيدين في هذه المجموعة حتى الآن.</p>
        </div>
      ) : (
        <div className="card" style={{ padding: '1rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', padding: '0 0.5rem' }}>
            <span style={{ fontWeight: 700, fontSize: '1.1rem' }}>
              كشف حضور: {selectedSession?.groupName} ({roster.length} طالب)
            </span>
            <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
              أزرار سريعة 48px مخصصة للمس بالهاتف والتابلت
            </span>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.65rem' }}>
            {roster.map(student => (
              <div
                key={student.studentId}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '0.75rem 1rem',
                  borderRadius: 'var(--border-radius-md)',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                  flexWrap: 'wrap',
                  gap: '0.75rem',
                }}
              >
                <div>
                  <h4 style={{ fontSize: '1.05rem', margin: 0 }}>{student.fullName}</h4>
                  <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                    كود: {student.studentCode} {student.phoneNumber ? `| هاتف: ${student.phoneNumber}` : ''}
                  </span>
                </div>

                {/* 48px Touch-First Toggle Chips */}
                <div className="fast-tap-container">
                  <button
                    type="button"
                    className={`tap-chip chip-present ${student.status === 'Present' ? 'active' : ''}`}
                    onClick={() => handleStatusChange(student.studentId, 'Present')}
                    title="حاضر"
                  >
                    حاضر
                  </button>
                  <button
                    type="button"
                    className={`tap-chip chip-late ${student.status === 'Late' ? 'active' : ''}`}
                    onClick={() => handleStatusChange(student.studentId, 'Late')}
                    title="متأخر"
                  >
                    متأخر
                  </button>
                  <button
                    type="button"
                    className={`tap-chip chip-excused ${student.status === 'Excused' ? 'active' : ''}`}
                    onClick={() => handleStatusChange(student.studentId, 'Excused')}
                    title="معذور"
                  >
                    معذور
                  </button>
                  <button
                    type="button"
                    className={`tap-chip chip-absent ${student.status === 'Absent' ? 'active' : ''}`}
                    onClick={() => handleStatusChange(student.studentId, 'Absent')}
                    title="غائب"
                  >
                    غائب
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};
