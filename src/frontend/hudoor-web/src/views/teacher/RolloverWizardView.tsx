import React, { useEffect, useState } from 'react';
import { api, ApiError } from '../../services/apiClient';
import type { AcademicGroup, RolloverResultDto, RolloverSummary, StudentSummary } from '../../types';

interface RolloverWizardViewProps {
  onNavigate: (view: string) => void;
}

export const RolloverWizardView: React.FC<RolloverWizardViewProps> = ({ onNavigate }) => {
  const [activeYear, setActiveYear] = useState<{ id: string; name: string } | null>(null);
  const [summary, setSummary] = useState<RolloverSummary | null>(null);
  const [students, setStudents] = useState<StudentSummary[]>([]);
  const [groups, setGroups] = useState<AcademicGroup[]>([]);
  const [loading, setLoading] = useState(true);

  // New year form
  const [newYearName, setNewYearName] = useState('2026/2027');
  const [startDate, setStartDate] = useState('2026-09-01');
  const [endDate, setEndDate] = useState('2027-06-30');

  // Progression decisions: map studentId -> { action: 'Promote' | 'Repeat' | 'Depart', newGrade?: string, targetGroupId?: string }
  const [decisions, setDecisions] = useState<Record<string, { action: string; newGrade: string; targetGroupId?: string }>>({});
  const [executing, setExecuting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<RolloverResultDto | null>(null);

  useEffect(() => {
    // 1. Fetch active academic year
    api.get<any[]>('/teacher/academic-years')
      .then(async years => {
        const active = years.find(y => y.isActive);
        if (active) {
          setActiveYear(active);
          const [sumRes, studentsRes, groupsRes] = await Promise.all([
            api.get<RolloverSummary>(`/teacher/academic-years/${active.id}/rollover-summary`),
            api.get<StudentSummary[]>('/teacher/students'),
            api.get<AcademicGroup[]>('/teacher/groups'),
          ]);
          setSummary(sumRes);
          setStudents(studentsRes);
          setGroups(groupsRes);

          // Initialize default decisions: Promote all
          const initialDecisions: Record<string, { action: string; newGrade: string }> = {};
          studentsRes.forEach(s => {
            let nextGrade = s.currentGrade || '1st Secondary';
            if (s.currentGrade === '1st Secondary') nextGrade = '2nd Secondary';
            else if (s.currentGrade === '2nd Secondary') nextGrade = '3rd Secondary';
            else if (s.currentGrade === '3rd Secondary') nextGrade = 'Graduated';

            initialDecisions[s.id] = {
              action: nextGrade === 'Graduated' ? 'Depart' : 'Promote',
              newGrade: nextGrade,
            };
          });
          setDecisions(initialDecisions);
        }
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const handleDecisionChange = (studentId: string, action: string, newGrade?: string, targetGroupId?: string) => {
    setDecisions(prev => ({
      ...prev,
      [studentId]: {
        action,
        newGrade: newGrade !== undefined ? newGrade : (prev[studentId]?.newGrade || ''),
        targetGroupId: targetGroupId !== undefined ? targetGroupId : prev[studentId]?.targetGroupId,
      },
    }));
  };

  const handleExecute = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setExecuting(true);

    try {
      const progressions = students.map(s => {
        const dec = decisions[s.id] || { action: 'Promote', newGrade: s.currentGrade || '' };
        return {
          studentId: s.id,
          action: dec.action,
          newGrade: dec.newGrade || undefined,
          targetGroupId: dec.targetGroupId || undefined,
        };
      });

      const res = await api.post<RolloverResultDto>('/teacher/academic-years/rollover', {
        newAcademicYearName: newYearName.trim(),
        startDate: new Date(startDate).toISOString(),
        endDate: new Date(endDate).toISOString(),
        studentProgressions: progressions,
      });

      setResult(res);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('حدث خطأ أثناء تنفيذ عملية الترحيل');
      }
    } finally {
      setExecuting(false);
    }
  };

  if (result) {
    return (
      <div className="card" style={{ maxWidth: '650px', margin: '2rem auto', textAlign: 'center', padding: '2.5rem' }}>
        <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>🎉</div>
        <h2 style={{ color: 'var(--status-present)', marginBottom: '0.5rem' }}>
          تم ترحيل العام الدراسي بنجاح!
        </h2>
        <p style={{ fontSize: '0.95rem', marginBottom: '1.5rem', color: 'var(--text-secondary)' }}>
          تم أرشفة العام السابق وتفعيل العام الجديد <strong>{result.newAcademicYearName}</strong>.
          كافة سجلات الحضور السابقة محفوظة 100% بدون أي حذف بيانات (Zero Data Loss).
        </p>

        <div className="metrics-grid" style={{ marginBottom: '1.5rem' }}>
          <div className="metric-card">
            <span className="metric-label">تمت ترقيتهم</span>
            <span className="metric-value" style={{ color: 'var(--status-present)' }}>{result.promotedCount}</span>
          </div>
          <div className="metric-card">
            <span className="metric-label">تمت إعادتهم</span>
            <span className="metric-value" style={{ color: 'var(--status-late)' }}>{result.repeatedCount}</span>
          </div>
          <div className="metric-card">
            <span className="metric-label">تخرجوا / غادروا</span>
            <span className="metric-value" style={{ color: 'var(--status-absent)' }}>{result.departedCount}</span>
          </div>
        </div>

        <button
          type="button"
          className="btn btn-primary"
          style={{ width: '100%', minHeight: '48px' }}
          onClick={() => onNavigate('dashboard')}
        >
          العودة للوحة التحكم
        </button>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      <div className="card">
        <h2 style={{ fontSize: '1.4rem', marginBottom: '0.35rem' }}>
          معالج الانتقال للعام الدراسي الجديد (Academic Year Rollover)
        </h2>
        <p style={{ fontSize: '0.9rem' }}>
          ترحيل سلس ومؤتمت ينقل طلابك للمراحل الجديدة مع الحفاظ الكامل على كافة جلسات الحضور وتاريخ الأعوام السابقة (Rule BR-ROL-090).
        </p>
      </div>

      {loading ? (
        <p>جاري تحميل بيانات العام الحالي...</p>
      ) : !activeYear ? (
        <div className="card" style={{ textAlign: 'center', padding: '3rem' }}>
          <p>لا يوجد عام دراسي نشط حاليًا لبدء عملية الترحيل منه.</p>
        </div>
      ) : (
        <form onSubmit={handleExecute} style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          {error && (
            <div style={{ padding: '0.75rem 1rem', borderRadius: 'var(--border-radius-md)', backgroundColor: 'var(--status-absent-subtle)', color: 'var(--status-absent)', fontWeight: 600 }}>
              {error}
            </div>
          )}

          {/* Step 1: Summary of Current Year */}
          <div className="card">
            <h3 style={{ marginBottom: '1rem' }}>1. ملخص العام الدراسي الحالي ({activeYear.name})</h3>
            {summary && (
              <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
                <div style={{ padding: '0.75rem 1rem', backgroundColor: 'var(--bg-secondary)', borderRadius: 'var(--border-radius-md)' }}>
                  إجمالي المجموعات: <strong>{summary.totalGroups}</strong>
                </div>
                <div style={{ padding: '0.75rem 1rem', backgroundColor: 'var(--bg-secondary)', borderRadius: 'var(--border-radius-md)' }}>
                  إجمالي الطلاب المقيدين: <strong>{summary.totalEnrolledStudents}</strong>
                </div>
                {summary.studentsByGrade.map(g => (
                  <div key={g.grade} style={{ padding: '0.75rem 1rem', backgroundColor: 'var(--bg-secondary)', borderRadius: 'var(--border-radius-md)' }}>
                    {g.grade}: <strong>{g.studentCount} طالب</strong>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Step 2: Configure New Academic Year */}
          <div className="card">
            <h3 style={{ marginBottom: '1rem' }}>2. إعدادات العام الدراسي الجديد</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1rem' }}>
              <div className="form-group">
                <label className="form-label">اسم العام الدراسي الجديد *</label>
                <input
                  type="text"
                  className="form-input"
                  value={newYearName}
                  onChange={e => setNewYearName(e.target.value)}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">تاريخ البداية *</label>
                <input
                  type="date"
                  className="form-input"
                  value={startDate}
                  onChange={e => setStartDate(e.target.value)}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">تاريخ النهاية *</label>
                <input
                  type="date"
                  className="form-input"
                  value={endDate}
                  onChange={e => setEndDate(e.target.value)}
                  required
                />
              </div>
            </div>
          </div>

          {/* Step 3: Student Progression Table */}
          <div className="card">
            <h3 style={{ marginBottom: '0.5rem' }}>3. تحديد مصير الطلاب في العام الجديد ({students.length} طالب)</h3>
            <p style={{ fontSize: '0.85rem', marginBottom: '1rem' }}>
              حدد لكل طالب ما إذا كان سيتم ترقيته، أو إعادة السنة، أو مغادرة المركز.
            </p>

            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'start' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid var(--border-color)', color: 'var(--text-secondary)' }}>
                    <th style={{ padding: '0.75rem', textAlign: 'start' }}>اسم الطالب</th>
                    <th style={{ padding: '0.75rem', textAlign: 'start' }}>الصف الحالي</th>
                    <th style={{ padding: '0.75rem', textAlign: 'center' }}>الإجراء</th>
                    <th style={{ padding: '0.75rem', textAlign: 'start' }}>الصف الجديد</th>
                    <th style={{ padding: '0.75rem', textAlign: 'start' }}>المجموعة المستهدفة</th>
                  </tr>
                </thead>
                <tbody>
                  {students.map(st => {
                    const dec = decisions[st.id] || { action: 'Promote', newGrade: st.currentGrade || '' };
                    return (
                      <tr key={st.id} style={{ borderBottom: '1px solid var(--border-color-subtle)' }}>
                        <td style={{ padding: '0.75rem', fontWeight: 700 }}>
                          {st.fullName}
                          <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>{st.studentCode}</div>
                        </td>
                        <td style={{ padding: '0.75rem' }}>{st.currentGrade || '—'}</td>
                        <td style={{ padding: '0.75rem', textAlign: 'center' }}>
                          <select
                            className="form-select"
                            value={dec.action}
                            onChange={e => handleDecisionChange(st.id, e.target.value)}
                            style={{ minWidth: '130px', minHeight: '38px', padding: '0.3rem 0.6rem' }}
                          >
                            <option value="Promote">ترقية (Promote)</option>
                            <option value="Repeat">إعادة (Repeat)</option>
                            <option value="Depart">مغادرة (Depart)</option>
                          </select>
                        </td>
                        <td style={{ padding: '0.75rem' }}>
                          {dec.action === 'Promote' ? (
                            <input
                              type="text"
                              className="form-input"
                              value={dec.newGrade}
                              onChange={e => handleDecisionChange(st.id, dec.action, e.target.value)}
                              style={{ minHeight: '38px', padding: '0.3rem 0.6rem', maxWidth: '180px' }}
                            />
                          ) : (
                            <span style={{ color: 'var(--text-muted)' }}>{st.currentGrade}</span>
                          )}
                        </td>
                        <td style={{ padding: '0.75rem' }}>
                          <select
                            className="form-select"
                            value={dec.targetGroupId || ''}
                            onChange={e => handleDecisionChange(st.id, dec.action, undefined, e.target.value || undefined)}
                            style={{ minWidth: '170px', minHeight: '38px', padding: '0.3rem 0.6rem' }}
                          >
                            <option value="">-- بدون مجموعة الآن --</option>
                            {groups.map(g => (
                              <option key={g.id} value={g.id}>
                                {g.name}
                              </option>
                            ))}
                          </select>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>

          <button
            type="submit"
            className="btn btn-primary"
            disabled={executing}
            style={{ minHeight: '52px', fontSize: '1.1rem' }}
          >
            {executing ? 'جاري تنفيذ الترحيل...' : 'تنفيذ الترحيل وبدء العام الدراسي الجديد 🚀'}
          </button>
        </form>
      )}
    </div>
  );
};
