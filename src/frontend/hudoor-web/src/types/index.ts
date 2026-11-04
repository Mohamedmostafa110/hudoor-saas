export type UserRole = 'PlatformAdmin' | 'Teacher' | 'Student';

export interface AuthUser {
  id: string;
  username?: string;
  studentCode?: string;
  fullName: string;
  email?: string;
  role: UserRole;
  mustChangePassword?: boolean;
}

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  role?: string;
  accountType?: string;
  usernameOrCode?: string;
  username?: string;
  studentCode?: string;
  fullName: string;
  teacherId?: string;
  studentId?: string;
  mustChangePassword?: boolean;
}

export interface StudentSummary {
  id: string;
  studentCode: string;
  fullName: string;
  phoneNumber?: string;
  parentPhoneNumber?: string;
  currentGrade?: string;
  isActive: boolean;
  mustChangePassword: boolean;
  enrolledGroupsCount: number;
  createdAtUtc: string;
}

export interface StudentCreatedResponse {
  id: string;
  studentCode: string;
  temporaryPassword: string;
  fullName: string;
  currentGrade?: string;
  expiresAtUtc?: string;
  initialGroupId?: string;
  initialGroupName?: string;
}

export interface ResetPasswordResponse {
  studentId: string;
  studentCode: string;
  temporaryPassword: string;
  expiresAtUtc: string;
}

export interface AcademicGroup {
  id: string;
  name: string;
  subjectId: string;
  subjectName: string;
  academicYearId: string;
  academicYearName: string;
  maxCapacity?: number;
  enrolledStudentsCount: number;
  isArchived: boolean;
}

export interface AttendanceSession {
  id: string;
  groupId: string;
  groupName: string;
  subjectName: string;
  title: string;
  sessionDateUtc: string;
  status: 'Draft' | 'Active' | 'Closed' | 'Archived';
  enrolledStudentsCount: number;
  presentCount: number;
  lateCount: number;
  excusedCount: number;
  absentCount: number;
  unrecordedCount: number;
}

export interface SessionQrToken {
  sessionId: string;
  qrToken: string;
  remainingSeconds: number;
  expiresAtUtc: string;
}

export interface RosterStudent {
  studentId: string;
  studentCode: string;
  fullName: string;
  phoneNumber?: string;
  attendanceRecordId?: string;
  status: 'Present' | 'Late' | 'Excused' | 'Absent' | 'Unrecorded';
  method?: string;
  recordedAtUtc?: string;
  minutesLate?: number;
  notes?: string;
}

export interface StudentAttendanceSummary {
  totalSessions: number;
  presentCount: number;
  lateCount: number;
  excusedCount: number;
  absentCount: number;
  attendancePercentage: number;
}

export interface StudentAttendanceHistoryItem {
  attendanceRecordId: string;
  sessionId: string;
  sessionTitle: string;
  groupName: string;
  subjectName: string;
  sessionDateUtc: string;
  status: string;
  method: string;
  minutesLate?: number;
  recordedAtUtc: string;
}

export interface TeacherAnalyticsSummary {
  totalStudents: number;
  activeStudents: number;
  totalGroups: number;
  totalSessions: number;
  overallAttendanceRate: number;
  totalPresentCount: number;
  totalLateCount: number;
  totalExcusedCount: number;
  totalAbsentCount: number;
  atRiskStudentsCount: number;
  groupSummaries: GroupSummaryItem[];
}

export interface GroupSummaryItem {
  groupId: string;
  groupName: string;
  subjectName: string;
  enrolledStudentsCount: number;
  totalSessionsCount: number;
  attendanceRate: number;
  presentCount: number;
  lateCount: number;
  excusedCount: number;
  absentCount: number;
}

export interface AtRiskStudent {
  studentId: string;
  studentCode: string;
  fullName: string;
  phoneNumber?: string;
  parentPhoneNumber?: string;
  currentGrade?: string;
  groupName: string;
  totalExpectedSessions: number;
  attendedSessions: number;
  absentSessions: number;
  attendanceRate: number;
  lastAttendedDateUtc?: string;
}

export interface GroupAttendanceMatrix {
  groupId: string;
  groupName: string;
  subjectName: string;
  sessions: { sessionId: string; title: string; sessionDateUtc: string }[];
  students: {
    studentId: string;
    studentCode: string;
    fullName: string;
    sessionStatuses: Record<string, string>;
    totalPresent: number;
    totalLate: number;
    totalExcused: number;
    totalAbsent: number;
    attendanceRate: number;
  }[];
}

export interface RolloverSummary {
  currentAcademicYearId: string;
  currentAcademicYearName: string;
  startDate: string;
  endDate: string;
  totalGroups: number;
  totalEnrolledStudents: number;
  studentsByGrade: { grade: string; studentCount: number }[];
}

export interface RolloverResultDto {
  previousAcademicYearId: string;
  previousAcademicYearName: string;
  newAcademicYearId: string;
  newAcademicYearName: string;
  promotedCount: number;
  repeatedCount: number;
  departedCount: number;
  totalProcessed: number;
  executedAtUtc: string;
  message: string;
}
